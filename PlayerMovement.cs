using System;
using System.Linq;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerMovement : Component
{
    // Movement Properties

    [Property] public float GroundControl { get; set; } = 4.0f;
    [Property] public float AirControl { get; set; } = 0.1f;
    [Property] public float MaxForce { get; set; } = 50f;
    [Property] public float Speed { get; set; } = 160f;
    [Property] public float RunSpeed { get; set; } = 290f;
    [Property] public float DuckSpeed { get; set; } = 90f;
    [Property] public float JumpForce { get; set; } = 400f;

    [Property] public float StandingHeight { get; set; } = 64f;
    [Property] public float DuckingHeight { get; set; } = 32f;

    // Object References
    [Property] public GameObject Head {get; set; }
    [Property] public GameObject Body {get; set; }

    // Member Variables
    public Vector3 WishVelocity = Vector3.Zero;

    [Sync] public bool IsDucking {get; set;} = false;
    [Sync] public bool IsSprinting {get; set;} = false;

    [Sync] Angles targetAngle {get; set;} = Angles.Zero;

    SkinnedModelRenderer BodyRenderer {get; set;}

    private CharacterController charactercontroller;
    private CitizenAnimationHelper animationHelper;

    public static PlayerMovement Local
    {
        get
        {
            if(!_local.IsValid())
            {
                _local = Game.ActiveScene.GetAllComponents<PlayerMovement>().FirstOrDefault(x => x.Network.IsOwner);
            }
            return _local;
        }
    }
    private static PlayerMovement _local = null;

    protected override void OnAwake()
    {
        charactercontroller = Components.Get<CharacterController>();
        animationHelper= Components.Get<CitizenAnimationHelper>();
        BodyRenderer = Body.Components.Get<SkinnedModelRenderer>();

        GameObject?.Tags.Add( "player" );
    }

    protected override void OnUpdate()
    {
        if (!Network.IsProxy)
        {
            if (Tags.Has("NoInput"))
            {
                // Don’t process any input or movement while dead
                return;
            }
            // Set our sprinting and Ducking states
            UpdateDuck();
            IsSprinting = Input.Down( "Run" );
            if( Input.Pressed("Jump") ) Jump();

            targetAngle = new Angles( 0, Head.Transform.Rotation.Yaw(), 0).ToRotation();
        }

        RotateBody();
        UpdateAnimations();
    }

    protected override void OnFixedUpdate()
    {
        if(Network.IsProxy) return;

        BuildWishVelocity();
        Move();        
    }

    void BuildWishVelocity()
    {
        WishVelocity = 0;

        var rot = Head.WorldRotation;
        if ( Input.Down( "Forward" ) ) WishVelocity += rot.Forward;
        if ( Input.Down( "Backward" ) ) WishVelocity += rot.Backward;
        if ( Input.Down( "Left" ) ) WishVelocity += rot.Left;
        if ( Input.Down( "Right" ) ) WishVelocity += rot.Right;

        WishVelocity = WishVelocity.WithZ(0);
        if(!WishVelocity.IsNearZeroLength) WishVelocity = WishVelocity.Normal;

        if ( IsDucking ) WishVelocity *= DuckSpeed;
        else if ( IsSprinting ) WishVelocity *= RunSpeed;
        else WishVelocity *= Speed;
    }

    void Move()
    {
        // Get gravity from our scene
        var gravity = Scene.PhysicsWorld.Gravity;

        if( charactercontroller.IsOnGround )
        {
            // Apply Friction/Acceleration
            charactercontroller.Velocity = charactercontroller.Velocity.WithZ( 0 );
            charactercontroller.Accelerate( WishVelocity );
            charactercontroller.ApplyFriction( GroundControl );
        }
        else
        {
            // Apply air control / gravity
            charactercontroller.Velocity += gravity * Time.Delta * 0.5f;
            charactercontroller.Accelerate( WishVelocity.ClampLength( MaxForce ) );
            charactercontroller.ApplyFriction( AirControl );
        }

        // Move the character controller
        charactercontroller.Move();

        // Apply the second half of gravity after movement
        if(!charactercontroller.IsOnGround)
        {
            charactercontroller.Velocity += gravity *Time.Delta * 0.5f;
        }
        else
        {
            charactercontroller.Velocity = charactercontroller.Velocity.WithZ ( 0 );
        }
    }

    void RotateBody()
    {
        if(Body is null) return;

        float rotateDifference = Body.WorldRotation.Distance( targetAngle );

        if (rotateDifference > 50f || charactercontroller.Velocity.Length > 10f )
        {
            Body.WorldRotation = Rotation.Lerp(Body.WorldRotation, targetAngle, Time.Delta * 2f);
        }
    }

    void Jump()
    {
        if(!charactercontroller.IsOnGround) return;

        charactercontroller.Punch(Vector3.Up * JumpForce);
        BroadcastJumpAnimation();
    }

    void UpdateAnimations()
    {
        BodyRenderer.RenderType = Network.IsProxy ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;

        if(animationHelper is null) return;

        animationHelper.WithWishVelocity( WishVelocity);
        animationHelper.WithVelocity( charactercontroller.Velocity );
        animationHelper.AimAngle = Head.WorldRotation;
        animationHelper.IsGrounded = charactercontroller.IsOnGround;
        animationHelper.WithLook(Head.WorldRotation.Forward, 1f, 0.75f, 0.5f);
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
        animationHelper.DuckLevel = IsDucking ? 1f : 0f;

    }

    void UpdateDuck()
    {
        if ( charactercontroller is null ) return;

        // Start crouching
        if ( Input.Pressed( "Duck" ) && !IsDucking )
        {
            IsDucking = true;
            charactercontroller.Height = DuckingHeight;
        }

        // Try to stand back up when not holding Duck anymore
        if ( !Input.Down( "Duck" ) && IsDucking )
        {
            if ( CanStandUp() )
            {
                IsDucking = false;
                charactercontroller.Height = StandingHeight;
            }
        }
    }

    bool CanStandUp()
    {
        // Start trace from the crouched head height upwards to full standing height
        var origin = charactercontroller.GameObject.WorldPosition + Vector3.Up * DuckingHeight;
        var target = charactercontroller.GameObject.WorldPosition + Vector3.Up * StandingHeight;

        var tr = Scene.Trace
            .Ray( origin, target )
            .Radius( charactercontroller.Radius * 0.9f )
            .WithoutTags( "player" )   // ignores ourself
            .Run();

        return !tr.Hit;
    }

    [Rpc.Broadcast]
    void BroadcastJumpAnimation()
    {
        animationHelper?.TriggerJump();
    }
}
