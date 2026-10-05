using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class FawnBrain : MonoBehaviour
{
    public enum Action { Idle, Grazing, Walking, Running, Alert, Fleeing }

    [Header("Routine")]
    [SerializeField] float roamRadius = 15f;
    [SerializeField] Vector2 idleSeconds = new Vector2(3f, 7f);
    [SerializeField, Range(0, 1)] float grazeChance = .6f;
    [SerializeField, Range(0, 1)] float runChance = .3f;
    [SerializeField] int seed = 731;

    [Header("Movement")]
    [SerializeField] float walkSpeed = .8f;
    [SerializeField] float runSpeed = 7f;
    [SerializeField] float turnSpeed = 90f;

    [Header("Player reactions")]
    [SerializeField] float alertDistance = 8f;
    [SerializeField] float spotDistance = 6f;
    [SerializeField] float fleeDistance = 3.5f;
    [SerializeField] float runningThreatDistance = 6f;
    [SerializeField] float safeDistance = 11f;
    [SerializeField] float calmSeconds = 4f;

    public Action CurrentAction { get; private set; }
    public Vector3 Home => home;
    public float RoamRadius => roamRadius;

    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int Grazing = Animator.StringToHash("Grazing");
    static readonly int Alert = Animator.StringToHash("Alert");
    static readonly int Spotted = Animator.StringToHash("Spotted");
    Transform head, upperNeck;
    float lookYaw, lookPitch, yawVelocity, pitchVelocity, lookWeight;
    public bool IsTrackingPlayer { get; private set; }
    PlayerMovement player;
    readonly RaycastHit[] sightHits = new RaycastHit[24];
    float senseAt, lastThreatTime = -100f, fleeRepathAt;
    bool seesPlayer;
    Animator animator;
    NavMeshAgent agent;
    Terrain terrain;
    System.Random random;
    NavMeshPath path;
    Vector3 home;
    float remaining, movingTime, runAt;
    bool grazeNext, runPlanned, stopping;
    Action stoppedAction;
    float stoppedIdleDuration;
    const float Braking = 12f;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        terrain = Terrain.activeTerrain;
        random = new System.Random(seed);
        path = new NavMeshPath();
        home = transform.position;
        agent.updateRotation = false;
        agent.autoBraking = true;
        animator.applyRootMotion = false;
        player = FindAnyObjectByType<PlayerMovement>();
        var bones = animator.GetComponentsInChildren<Transform>();
        foreach (var bone in bones)
        {
            if (bone.name == "head") head = bone;
            if (bone.name == "neck.001" || bone.name == "neck001") upperNeck = bone;
        }
        EnterIdle();
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;
        float dt = Time.deltaTime;
        SensePlayer();
        if (player)
        {
            float distance = FlatDistance(transform.position, player.transform.position);
            bool threatened = seesPlayer || distance < 2f;
            bool urgent = threatened && (distance < fleeDistance ||
                (distance < runningThreatDistance && player.PlanarSpeed > player.WalkSpeed + .2f));
            if (urgent && CurrentAction != Action.Fleeing) EnterFlee();
            else if (threatened && distance < alertDistance && CurrentAction != Action.Fleeing && CurrentAction != Action.Alert) EnterAlert();
        }
        if (stopping)
        {
            agent.acceleration = Braking;
            agent.speed = 0;
            if (agent.velocity.sqrMagnitude <= .0064f)
            {
                stopping = false;
                if (stoppedAction == Action.Alert) EnterAlert();
                else EnterIdle(stoppedIdleDuration);
            }
            UpdateGait(dt);
            return;
        }
        switch (CurrentAction)
        {
            case Action.Idle:
                remaining -= dt;
                if (remaining <= 0)
                {
                    if (grazeNext) EnterGrazing();
                    else BeginWalk();
                }
                break;
            case Action.Grazing:
                // Finish the whole lower, feed and raise sequence before moving.
                if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("Grazing") &&
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                {
                    animator.SetBool(Grazing, false);
                    EnterIdle(.65f);
                    grazeNext = false;
                }
                break;
            case Action.Alert:
                IsTrackingPlayer = seesPlayer && player && FlatDistance(transform.position, player.transform.position) <= spotDistance;
                animator.SetBool(Spotted, IsTrackingPlayer);
                if (Time.time - lastThreatTime > calmSeconds && remaining <= 0) EnterIdle();
                remaining -= dt;
                break;
            case Action.Fleeing:
                Flee(dt);
                break;
            case Action.Walking:
            case Action.Running:
                Move(dt);
                break;
        }
        UpdateGait(dt);
    }

    void EnterIdle(float duration = -1)
    {
        if (BrakeBefore(Action.Idle, duration)) return;
        CurrentAction = Action.Idle;
        IsTrackingPlayer = false;
        animator.SetBool(Spotted, false);
        if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
        animator.SetBool(Grazing, false);
        animator.SetBool(Alert, false);
        remaining = duration >= 0 ? duration : Range(idleSeconds.x, idleSeconds.y);
        grazeNext = random.NextDouble() < grazeChance;
    }

    void EnterGrazing()
    {
        CurrentAction = Action.Grazing;
        agent.isStopped = true;
        animator.SetBool(Grazing, true);
    }

    void BeginWalk()
    {
        for (int i = 0; i < 24; i++)
        {
            float angle = i < 12 ? Range(-55f, 55f) : Range(-135f, 135f);
            Vector3 point = transform.position + Quaternion.Euler(0, angle, 0) * transform.forward * Range(5f, 11f);
            if (Vector3.Distance(new Vector3(point.x, home.y, point.z), home) > roamRadius) continue;
            if (!NavMesh.SamplePosition(point, out var hit, 1.5f, agent.areaMask)) continue;
            if (terrain && Mathf.Abs(hit.position.y - terrain.SampleHeight(hit.position) - terrain.transform.position.y) > .3f) continue;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            bool inside = true;
            foreach (var corner in path.corners)
            {
                if (Vector2.Distance(new Vector2(corner.x, corner.z), new Vector2(home.x, home.z)) > roamRadius) inside = false;
                if (terrain && Mathf.Abs(corner.y - terrain.SampleHeight(corner) - terrain.transform.position.y) > .4f) inside = false;
            }
            if (!inside) continue;
            agent.speed = walkSpeed;
            agent.isStopped = false;
            agent.SetPath(path);
            CurrentAction = Action.Walking;
            movingTime = 0;
            runAt = Range(1.5f, 3f);
            runPlanned = random.NextDouble() < runChance;
            remaining = Range(1.2f, 2.4f);
            return;
        }
        EnterIdle(2f);
    }

    void Move(float dt)
    {
        if (agent.pathPending) return;
        if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete || agent.remainingDistance <= agent.stoppingDistance + .15f)
        {
            EnterIdle();
            return;
        }
        movingTime += dt;
        if (CurrentAction == Action.Walking && runPlanned && movingTime >= runAt && agent.remainingDistance > 5f)
        {
            CurrentAction = Action.Running;
            runPlanned = false;
        }
        if (CurrentAction == Action.Running)
        {
            remaining -= dt;
            if (remaining <= 0 || agent.remainingDistance < 3f) CurrentAction = Action.Walking;
        }
        // Turn with the actual travel direction while the gait continues.
        Vector3 direction = agent.velocity;
        direction.y = 0;
        if (direction.sqrMagnitude > .01f)
        {
            var rotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, turnSpeed * dt);
        }
        float wanted = CurrentAction == Action.Running ? runSpeed : walkSpeed;
        bool braking = wanted < agent.velocity.magnitude;
        agent.acceleration = braking ? Braking : CurrentAction == Action.Running ? 10f : 2.5f;
        agent.speed = Mathf.MoveTowards(agent.speed, wanted, (braking ? Braking : CurrentAction == Action.Running ? 10f : 3f) * dt);
    }

    void SensePlayer()
    {
        if (!player || Time.time < senseAt) return;
        senseAt = Time.time + .15f;
        seesPlayer = false;
        float distance = FlatDistance(transform.position, player.transform.position);
        if (distance > (CurrentAction == Action.Fleeing ? safeDistance : alertDistance)) return;
        Vector3 eye = transform.position + Vector3.up * 1.2f;
        Vector3 target = player.transform.position + Vector3.up;
        Vector3 ray = target - eye;
        int count = Physics.RaycastNonAlloc(eye, ray.normalized, sightHits, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
        bool blocked = count == sightHits.Length;
        for (int i = 0; i < count; i++)
        {
            var hit = sightHits[i].transform;
            if (!hit.IsChildOf(transform) && !hit.IsChildOf(player.transform)) blocked = true;
        }
        seesPlayer = !blocked;
        if (seesPlayer) lastThreatTime = Time.time;
    }

    void EnterAlert()
    {
        if (BrakeBefore(Action.Alert)) return;
        CurrentAction = Action.Alert;
        IsTrackingPlayer = seesPlayer && player && FlatDistance(transform.position, player.transform.position) <= spotDistance;
        animator.SetBool(Spotted, IsTrackingPlayer);
        agent.isStopped = true;
        agent.ResetPath();
        animator.SetBool(Grazing, false);
        animator.SetBool(Alert, true);
        remaining = 2f;
    }

    void EnterFlee()
    {
        stopping = false;
        CurrentAction = Action.Fleeing;
        IsTrackingPlayer = false;
        animator.SetBool(Spotted, false);
        animator.SetBool(Grazing, false);
        animator.SetBool(Alert, false);
        agent.isStopped = false;
        fleeRepathAt = 0;
        lastThreatTime = Time.time;
    }

    void Flee(float dt)
    {
        float distance = FlatDistance(transform.position, player.transform.position);
        if (distance < 2f) lastThreatTime = Time.time;
        if (distance >= safeDistance && Time.time - lastThreatTime >= calmSeconds)
        {
            home = transform.position;
            EnterAlert();
            return;
        }
        if (Time.time >= fleeRepathAt)
        {
            fleeRepathAt = Time.time + 1f;
            if (!agent.hasPath || agent.remainingDistance < 2f || FlatDistance(agent.destination, player.transform.position) < distance + 3f)
                ChooseEscape();
        }
        agent.acceleration = 12f;
        agent.speed = Mathf.MoveTowards(agent.speed, runSpeed, 12f * dt);
        Vector3 direction = agent.velocity;direction.y = 0;
        if (direction.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
    }

    void ChooseEscape()
    {
        Vector3 away = transform.position - player.transform.position;away.y = 0;
        if (away.sqrMagnitude < .01f) away = transform.forward;
        away.Normalize();
        float currentDistance = FlatDistance(transform.position, player.transform.position);
        float bestScore = float.MinValue;
        Vector3 best = Vector3.zero;
        for (int i = 0; i < 15; i++)
        {
            float angle = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 12f;
            Vector3 point = transform.position + Quaternion.Euler(0, angle, 0) * away * Range(7f, 13f);
            if (!NavMesh.SamplePosition(point, out var hit, 2f, agent.areaMask)) continue;
            if (terrain && Mathf.Abs(hit.position.y - terrain.SampleHeight(hit.position) - terrain.transform.position.y) > .3f) continue;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float endDistance = FlatDistance(hit.position, player.transform.position);
            if (endDistance < currentDistance + 2f || path.corners.Length < 2) continue;
            float length = 0;bool valid = true;
            for (int c = 1; c < path.corners.Length; c++)
            {
                length += Vector3.Distance(path.corners[c - 1], path.corners[c]);
                if (terrain && Mathf.Abs(path.corners[c].y - terrain.SampleHeight(path.corners[c]) - terrain.transform.position.y) > .4f) valid = false;
            }
            Vector3 firstStep = path.corners[1] - transform.position;firstStep.y = 0;
            if (!valid || Vector3.Dot(firstStep.normalized, away) < -.1f) continue;
            float score = endDistance - length * .2f;
            if (score > bestScore) { bestScore = score;best = hit.position; }
        }
        if (bestScore > float.MinValue) agent.SetDestination(best);
        // A blocked edge must never send the deer back towards the threat.
        else if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance) agent.ResetPath();
    }

    bool BrakeBefore(Action next, float idleDuration = -1)
    {
        if (agent.velocity.sqrMagnitude <= .0064f) return false;
        stopping = true;
        stoppedAction = next;
        stoppedIdleDuration = idleDuration;
        agent.acceleration = Braking;
        agent.speed = 0;
        animator.SetBool(Alert, false);
        animator.SetBool(Grazing, false);
        return true;
    }

    void UpdateGait(float dt)
    {
        float speed = agent.velocity.magnitude;
        if (speed <= .08f) animator.SetFloat(Speed, 0);
        else animator.SetFloat(Speed, speed, stopping || speed < animator.GetFloat(Speed) ? .04f : .16f, dt);
    }

    void LateUpdate()
    {
        if (!head || !player) return;
        bool focusing = CurrentAction == Action.Alert && IsTrackingPlayer;
        lookWeight = Mathf.MoveTowards(lookWeight, focusing ? 1f : 0f, Time.deltaTime * 5f);
        if (lookWeight <= 0) return;
        Vector3 target = player.transform.position + Vector3.up * 1.2f - head.position;
        Vector3 flat = Vector3.ProjectOnPlane(target, Vector3.up);
        float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, Vector3.up), -90f, 90f);
        float pitch = Mathf.Clamp(-Mathf.Atan2(target.y, flat.magnitude) * Mathf.Rad2Deg, -12f, 12f);
        lookYaw = Mathf.SmoothDampAngle(lookYaw, yaw, ref yawVelocity, .12f);
        lookPitch = Mathf.SmoothDampAngle(lookPitch, pitch, ref pitchVelocity, .12f);
        float neckShare = upperNeck ? .3f : 0f;
        if (upperNeck) upperNeck.rotation = Quaternion.AngleAxis(lookYaw * neckShare * lookWeight, Vector3.up) * upperNeck.rotation;
        head.rotation = Quaternion.AngleAxis(lookPitch * lookWeight, transform.right) *
            Quaternion.AngleAxis(lookYaw * (1f - neckShare) * lookWeight, Vector3.up) * head.rotation;
    }

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}
