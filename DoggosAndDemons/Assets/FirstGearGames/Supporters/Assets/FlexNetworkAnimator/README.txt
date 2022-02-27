FlexNetworkAnimator

API
=====================================   
    public void SetAnimator(Animator animator)
        Sets which animator to use. You must call this with the appropriate animator on all clients and server. This change is not automatically synchronized.
    public void SetController(RuntimeAnimatorController controller)
        Sets which controller to use. You must call this with the appropriate controller on all clients and server. This change is not automatically synchronized.
    public void SetTrigger() //All variants of Unity API.
        Sets a trigger on the animator and sends it over the network.
    public void Play() //All variants of Unity API.
        Plays a state.
    public void Crossfad() //All variants of Unity API.
        Creates a crossfade from the current state to any other state.
        