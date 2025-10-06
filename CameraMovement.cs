using Sandbox;

public sealed class CameraMovement : Component
{
    [Property] public float Distance { get; set; } = 0f;

    public bool IsFirstPerson => Distance == 0f;

    private Vector3 CurrentOffset = Vector3.Zero;

    private CameraComponent Camera;
    private ModelRenderer BodyRenderer;

    private PlayerMovement Player { get; set; }
    private GameObject Body { get; set; }
    private GameObject Head { get; set; }

    protected override void OnAwake()
    {
        Camera = Components.Get<CameraComponent>();
    }

    protected override void OnUpdate()
    {
        // ✅ Correct use of static Local
        if (Player == null)
        {
            Player = PlayerMovement.Local;
            if (Player == null) return; // still not spawned

            Body = Player.Body;
            Head = Player.Head;
            return; // wait one frame so refs are safe
        }

        if (Network.IsProxy) return;
        if (Head == null || Camera == null) return;

        // Rotate the head based on the mouse movement
        var eyeAngles = Head.WorldRotation.Angles();
        eyeAngles.pitch += Input.MouseDelta.y * 0.1f;
        eyeAngles.yaw   -= Input.MouseDelta.x * 0.1f;
        eyeAngles.roll   = 0f;
        eyeAngles.pitch  = eyeAngles.pitch.Clamp(-89.9f, 89.9f);
        Head.WorldRotation = eyeAngles.ToRotation();

        // Set the current camera offset
        var targetOffset = Vector3.Zero;
        if (Player.IsDucking) targetOffset += Vector3.Down * 32f;
        CurrentOffset = Vector3.Lerp(CurrentOffset, targetOffset, Time.Delta * 10f);

        // Set the position of the camera
        var camPos = Head.WorldPosition + CurrentOffset;
        if (!IsFirstPerson)
        {
            var camForward = eyeAngles.ToRotation().Forward;
            var camTrace = Scene.Trace.Ray(camPos, camPos - (camForward * Distance))
                .WithoutTags("player", "trigger")
                .Run();

            camPos = camTrace.Hit
                ? camTrace.HitPosition + camTrace.Normal
                : camTrace.EndPosition;
        }

        Camera.WorldPosition = camPos;
        Camera.WorldRotation = eyeAngles.ToRotation();
    }
}
