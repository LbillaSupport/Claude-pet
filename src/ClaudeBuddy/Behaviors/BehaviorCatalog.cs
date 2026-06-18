using ClaudeBuddy.Core;

namespace ClaudeBuddy.Behaviors;

/// <summary>
/// The mascot's entire repertoire, expressed as data. Autonomous behaviours have a
/// positive <see cref="BehaviorDefinition.Weight"/> and are chosen by the selector;
/// reaction behaviours (weight 0) are only ever triggered explicitly by the
/// interaction layer (petting, Claude opening, being scared, …).
///
/// Mods and behaviour packs append to this catalogue at load time, so the character
/// can grow without touching engine code.
/// </summary>
public sealed class BehaviorCatalog
{
    private readonly Dictionary<string, BehaviorDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BehaviorDefinition> _all = new();

    public BehaviorCatalog()
    {
        foreach (BehaviorDefinition def in BuildDefaults())
        {
            Add(def);
        }
    }

    public IReadOnlyList<BehaviorDefinition> All => _all;

    public IEnumerable<BehaviorDefinition> Selectable => _all.Where(b => b.Weight > 0f);

    public BehaviorDefinition this[string id] => _byId[id];

    public bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BehaviorDefinition? def) =>
        _byId.TryGetValue(id, out def);

    /// <summary>Adds (or replaces) a definition — used by the mod loader.</summary>
    public void Add(BehaviorDefinition def)
    {
        _byId[def.Id] = def;
        _all.RemoveAll(b => b.Id.Equals(def.Id, StringComparison.OrdinalIgnoreCase));
        _all.Add(def);
    }

    private static IEnumerable<BehaviorDefinition> BuildDefaults() =>
    [
        // ---- Calm idles --------------------------------------------------
        new() { Id = "idle", DisplayName = "Idle", Animation = AnimationState.Idle, Category = BehaviorCategory.Idle, Weight = 6f, MinDuration = 2f, MaxDuration = 5f, Cooldown = 0.5f },
        new() { Id = "look-around", DisplayName = "Look Around", Animation = AnimationState.LookAround, Category = BehaviorCategory.Idle, Weight = 4f, MinDuration = 2.5f, MaxDuration = 5f, Mood = Mood.Curious },
        new() { Id = "sit", DisplayName = "Sit", Animation = AnimationState.Sit, Category = BehaviorCategory.Idle, Weight = 3f, MinDuration = 4f, MaxDuration = 9f },
        new() { Id = "stretch", DisplayName = "Stretch", Animation = AnimationState.Stretch, Category = BehaviorCategory.Idle, Weight = 2f, MinDuration = 1.8f, MaxDuration = 2.6f, Cooldown = 20f, Mood = Mood.Content },
        new() { Id = "think", DisplayName = "Think", Animation = AnimationState.Think, Category = BehaviorCategory.Idle, Weight = 2.5f, MinDuration = 3f, MaxDuration = 6f, Mood = Mood.Curious },
        new() { Id = "scratch-head", DisplayName = "Scratch Head", Animation = AnimationState.Think, Category = BehaviorCategory.Idle, Weight = 1.5f, MinDuration = 2f, MaxDuration = 3.5f, Mood = Mood.Confused, MoodIntensity = 0.5f },
        new() { Id = "read", DisplayName = "Read", Animation = AnimationState.Read, Category = BehaviorCategory.Idle, Weight = 2f, MinDuration = 6f, MaxDuration = 14f, Cooldown = 30f },
        new() { Id = "look-at-clock", DisplayName = "Look At The Clock", Animation = AnimationState.LookUp, Category = BehaviorCategory.Idle, Weight = 1.2f, MinDuration = 2f, MaxDuration = 3f, Mood = Mood.Curious },
        new() { Id = "watch-birds", DisplayName = "Watch Invisible Birds", Animation = AnimationState.LookUp, Category = BehaviorCategory.Idle, Weight = 1f, MinDuration = 3f, MaxDuration = 6f, Mood = Mood.Curious },
        new() { Id = "stare", DisplayName = "Stare Into Space", Animation = AnimationState.Idle, Category = BehaviorCategory.Idle, Weight = 1.4f, MinDuration = 4f, MaxDuration = 8f, Mood = Mood.Lazy },
        new() { Id = "balance-titles", DisplayName = "Balance On Window Titles", Animation = AnimationState.Stand, Category = BehaviorCategory.Idle, Weight = 0.8f, MinDuration = 3f, MaxDuration = 6f },
        new() { Id = "groom", DisplayName = "Tidy Up", Animation = AnimationState.Groom, Category = BehaviorCategory.Idle, Weight = 2f, MinDuration = 2.4f, MaxDuration = 4f, Cooldown = 16f, Mood = Mood.Content, MoodIntensity = 0.5f },
        new() { Id = "tap-foot", DisplayName = "Tap A Foot", Animation = AnimationState.TapFoot, Category = BehaviorCategory.Idle, Weight = 1.8f, MinDuration = 2.5f, MaxDuration = 5f, Cooldown = 12f, Mood = Mood.Content },
        new() { Id = "wiggle", DisplayName = "Happy Wiggle", Animation = AnimationState.Wiggle, Category = BehaviorCategory.Idle, Weight = 1.6f, MinDuration = 2f, MaxDuration = 4f, Cooldown = 14f, Mood = Mood.Happy, MoodIntensity = 0.6f },
        new() { Id = "daydream", DisplayName = "Daydream", Animation = AnimationState.LookUp, Category = BehaviorCategory.Idle, Weight = 1.5f, MinDuration = 4f, MaxDuration = 8f, Cooldown = 18f, Mood = Mood.Content, MoodIntensity = 0.5f, EnterParticle = ParticleKind.Magic },
        new() { Id = "ponder", DisplayName = "Ponder Deeply", Animation = AnimationState.Think, Category = BehaviorCategory.Idle, Weight = 1.6f, MinDuration = 3.5f, MaxDuration = 7f, Cooldown = 14f, Mood = Mood.Curious },
        new() { Id = "people-watch", DisplayName = "Watch The Cursor Pass By", Animation = AnimationState.LookAround, Category = BehaviorCategory.Idle, Weight = 1.4f, MinDuration = 3f, MaxDuration = 6f, Mood = Mood.Curious },
        new() { Id = "admire-view", DisplayName = "Admire The View", Animation = AnimationState.Stand, Category = BehaviorCategory.Idle, Weight = 1.2f, MinDuration = 4f, MaxDuration = 8f, Mood = Mood.Content },
        new() { Id = "wake-stretch", DisplayName = "Stretch In Place", Animation = AnimationState.Stretch, Category = BehaviorCategory.Idle, Weight = 1.4f, MinDuration = 1.8f, MaxDuration = 2.8f, Cooldown = 16f, Mood = Mood.Content },

        // ---- Exploration -------------------------------------------------
        new() { Id = "walk", DisplayName = "Walk", Animation = AnimationState.WalkRight, Movement = BehaviorMovement.Wander, Category = BehaviorCategory.Explore, Weight = 5f, MinDuration = 3f, MaxDuration = 7f, Cooldown = 2f },
        new() { Id = "inspect-windows", DisplayName = "Inspect Windows", Animation = AnimationState.WalkRight, Movement = BehaviorMovement.Wander, Category = BehaviorCategory.Explore, Weight = 2f, MinDuration = 4f, MaxDuration = 8f, Mood = Mood.Curious },
        new() { Id = "inspect-icons", DisplayName = "Inspect Desktop Icons", Animation = AnimationState.WalkRight, Movement = BehaviorMovement.Wander, Category = BehaviorCategory.Explore, Weight = 1.8f, MinDuration = 4f, MaxDuration = 8f, Mood = Mood.Curious },
        new() { Id = "pretend-cleaning", DisplayName = "Pretend Cleaning The Desktop", Animation = AnimationState.WalkRight, Movement = BehaviorMovement.Wander, Category = BehaviorCategory.Explore, Weight = 1.2f, MinDuration = 5f, MaxDuration = 9f, Mood = Mood.Proud },
        new() { Id = "peek-edge", DisplayName = "Peek From Screen Edge", Animation = AnimationState.LookAround, Movement = BehaviorMovement.EdgePeek, Category = BehaviorCategory.Explore, Weight = 1.5f, MinDuration = 4f, MaxDuration = 7f, Cooldown = 18f, Mood = Mood.Curious },
        new() { Id = "hide", DisplayName = "Hide Behind Icons", Animation = AnimationState.Sit, Category = BehaviorCategory.Explore, Weight = 1f, MinDuration = 3f, MaxDuration = 6f, Cooldown = 20f },
        new() { Id = "sit-taskbar", DisplayName = "Sit On Taskbar", Animation = AnimationState.Sit, Category = BehaviorCategory.Idle, Weight = 1.6f, MinDuration = 5f, MaxDuration = 12f },

        // ---- Active / acrobatic -----------------------------------------
        new() { Id = "run", DisplayName = "Run", Animation = AnimationState.RunRight, Movement = BehaviorMovement.Run, Category = BehaviorCategory.Active, Weight = 2.5f, MinDuration = 2f, MaxDuration = 4f, Cooldown = 6f, Mood = Mood.Excited, MoodIntensity = 0.6f },
        new() { Id = "jump", DisplayName = "Jump", Animation = AnimationState.Jump, Category = BehaviorCategory.Active, Weight = 2f, MinDuration = 0.9f, MaxDuration = 1.2f, Cooldown = 5f, Mood = Mood.Playful, EnterSound = "jump" },
        new() { Id = "spin", DisplayName = "Spin", Animation = AnimationState.Spin, Category = BehaviorCategory.Playful, Weight = 1.2f, MinDuration = 1.2f, MaxDuration = 1.8f, Cooldown = 12f, Mood = Mood.Playful, EnterParticle = ParticleKind.Sparkle },
        new() { Id = "roll", DisplayName = "Roll", Animation = AnimationState.Roll, Movement = BehaviorMovement.Run, Category = BehaviorCategory.Playful, Weight = 1f, MinDuration = 1.4f, MaxDuration = 2.2f, Cooldown = 16f, Mood = Mood.Playful },
        new() { Id = "trip", DisplayName = "Trip", Animation = AnimationState.Trip, Category = BehaviorCategory.Active, Weight = 0.7f, MinDuration = 1.2f, MaxDuration = 1.6f, Cooldown = 25f, Mood = Mood.Surprised, MoodIntensity = 0.7f },
        new() { Id = "climb", DisplayName = "Climb The Walls", Animation = AnimationState.Climb, Category = BehaviorCategory.Active, Weight = 5.5f, MinDuration = 120f, MaxDuration = 120f, Cooldown = 14f, Mood = Mood.Playful, MoodIntensity = 0.5f },
        new() { Id = "chase-sparkles", DisplayName = "Chase Floating Sparkles", Animation = AnimationState.RunRight, Movement = BehaviorMovement.Run, Category = BehaviorCategory.Playful, Weight = 1.2f, MinDuration = 2.5f, MaxDuration = 4f, Cooldown = 14f, Mood = Mood.Excited, EnterParticle = ParticleKind.Sparkle },

        // ---- Epic signature moves ---------------------------------------
        new() { Id = "backflip", DisplayName = "Backflip!", Animation = AnimationState.Jump, Category = BehaviorCategory.Active, Weight = 1.4f, MinDuration = 1f, MaxDuration = 1.3f, Cooldown = 16f, Mood = Mood.Excited, MoodIntensity = 0.85f, EnterParticle = ParticleKind.Sparkle, EnterSound = "jump" },
        new() { Id = "charge", DisplayName = "Power Up!", Animation = AnimationState.Charge, Category = BehaviorCategory.Playful, Weight = 1.1f, MinDuration = 1.5f, MaxDuration = 1.7f, Cooldown = 24f, Mood = Mood.Excited, MoodIntensity = 1f, ClimaxParticle = ParticleKind.Magic, EnterSound = "magic" },

        // ---- Social / curious about the cursor ---------------------------
        new() { Id = "watch-cursor", DisplayName = "Watch Cursor", Animation = AnimationState.LookAround, Category = BehaviorCategory.Social, Weight = 2.2f, MinDuration = 2.5f, MaxDuration = 5f, Mood = Mood.Curious },
        new() { Id = "inspect-mouse", DisplayName = "Inspect Mouse", Animation = AnimationState.Think, Movement = BehaviorMovement.ApproachCursor, Category = BehaviorCategory.Social, Weight = 1.8f, MinDuration = 3f, MaxDuration = 6f, Cooldown = 10f, Mood = Mood.Curious, MoodIntensity = 0.6f },
        new() { Id = "wave", DisplayName = "Wave", Animation = AnimationState.Wave, Category = BehaviorCategory.Social, Weight = 1.4f, MinDuration = 1.8f, MaxDuration = 2.6f, Cooldown = 14f, Mood = Mood.Happy, MoodIntensity = 0.6f, EnterParticle = ParticleKind.Sparkle },
        new() { Id = "laugh", DisplayName = "Laugh Alone", Animation = AnimationState.Happy, Category = BehaviorCategory.Social, Weight = 1f, MinDuration = 1.6f, MaxDuration = 2.4f, Cooldown = 18f, Mood = Mood.Happy, MoodIntensity = 0.7f },
        new() { Id = "proud", DisplayName = "Look Proud", Animation = AnimationState.Happy, Category = BehaviorCategory.Social, Weight = 0.9f, MinDuration = 2f, MaxDuration = 3.5f, Cooldown = 20f, Mood = Mood.Proud, MoodIntensity = 0.7f },
        new() { Id = "confused", DisplayName = "Act Confused", Animation = AnimationState.LookAround, Category = BehaviorCategory.Social, Weight = 0.9f, MinDuration = 2f, MaxDuration = 3.5f, Mood = Mood.Confused, MoodIntensity = 0.6f },

        // ---- Playful & whimsical ----------------------------------------
        new() { Id = "dance", DisplayName = "Dance", Animation = AnimationState.Dance, Category = BehaviorCategory.Playful, Weight = 1.6f, MinDuration = 3f, MaxDuration = 6f, Cooldown = 16f, Mood = Mood.Playful, MoodIntensity = 0.8f, EnterParticle = ParticleKind.Note, EnterSound = "happy" },
        new() { Id = "celebrate", DisplayName = "Celebrate Randomly", Animation = AnimationState.Celebrate, Category = BehaviorCategory.Playful, Weight = 1f, MinDuration = 2f, MaxDuration = 3f, Cooldown = 22f, Mood = Mood.Excited, MoodIntensity = 0.9f, EnterParticle = ParticleKind.Confetti, EnterSound = "celebrate" },
        new() { Id = "selfie", DisplayName = "Take A Selfie", Animation = AnimationState.Celebrate, Category = BehaviorCategory.Playful, Weight = 0.8f, MinDuration = 1.8f, MaxDuration = 2.4f, Cooldown = 25f, Mood = Mood.Happy, EnterParticle = ParticleKind.Sparkle },
        new() { Id = "butterfly", DisplayName = "Play With Tiny Butterfly", Animation = AnimationState.LookUp, Category = BehaviorCategory.Playful, Weight = 1f, MinDuration = 3f, MaxDuration = 6f, Cooldown = 18f, Mood = Mood.Playful, EnterParticle = ParticleKind.Magic },
        new() { Id = "find-flower", DisplayName = "Find A Flower", Animation = AnimationState.LookDown, Category = BehaviorCategory.Playful, Weight = 0.9f, MinDuration = 2.5f, MaxDuration = 4f, Cooldown = 20f, Mood = Mood.Happy, EnterParticle = ParticleKind.Magic },
        new() { Id = "umbrella", DisplayName = "Open A Tiny Umbrella", Animation = AnimationState.Idle, Category = BehaviorCategory.Playful, Weight = 0.6f, MinDuration = 4f, MaxDuration = 7f, Cooldown = 30f, Mood = Mood.Content },

        // ---- Sleepy ------------------------------------------------------
        new() { Id = "yawn", DisplayName = "Yawn", Animation = AnimationState.Yawn, Category = BehaviorCategory.Sleepy, Weight = 1.6f, MinDuration = 1.6f, MaxDuration = 2.2f, Cooldown = 12f, Mood = Mood.Sleepy, EnterSound = "yawn" },
        new() { Id = "drowse", DisplayName = "Get Drowsy", Animation = AnimationState.Sit, Category = BehaviorCategory.Sleepy, Weight = 1.4f, MinDuration = 4f, MaxDuration = 8f, Mood = Mood.Sleepy },
        new() { Id = "sleep", DisplayName = "Sleep", Animation = AnimationState.Sleep, Category = BehaviorCategory.Sleepy, Weight = 2.2f, MinDuration = 12f, MaxDuration = 35f, Cooldown = 8f, Mood = Mood.Sleepy, MoodIntensity = 0.8f, EnterSound = "sleep" },
        new() { Id = "dream", DisplayName = "Dream", Animation = AnimationState.Sleep, Category = BehaviorCategory.Sleepy, Weight = 1.2f, MinDuration = 14f, MaxDuration = 30f, Cooldown = 20f, Mood = Mood.Sleepy, EnterParticle = ParticleKind.Magic },
        new() { Id = "snore", DisplayName = "Snore", Animation = AnimationState.Sleep, Category = BehaviorCategory.Sleepy, Weight = 1f, MinDuration = 10f, MaxDuration = 22f, Cooldown = 16f, Mood = Mood.Sleepy, EnterSound = "sleep" },

        // ---- Routine props ----------------------------------------------
        new() { Id = "coffee", DisplayName = "Drink Coffee", Animation = AnimationState.Drink, Category = BehaviorCategory.Idle, Weight = 1.4f, MinDuration = 4f, MaxDuration = 7f, Cooldown = 18f, Mood = Mood.Content },

        // ---- Rare, happiness-gated treats -------------------------------
        new() { Id = "secret-dance", DisplayName = "Secret Dance", Animation = AnimationState.Dance, Category = BehaviorCategory.Special, Weight = 0.6f, MinDuration = 5f, MaxDuration = 8f, Cooldown = 40f, Mood = Mood.Excited, MoodIntensity = 1f, MinHappiness = 0.85f, EnterParticle = ParticleKind.Confetti, EnterSound = "magic" },

        // ---- Reaction-only behaviours (weight 0: never auto-selected) ----
        new() { Id = "pet", DisplayName = "Being Petted", Animation = AnimationState.Pet, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 1.4f, MaxDuration = 1.4f, Cooldown = 0f, Mood = Mood.Happy, MoodIntensity = 0.9f, EnterParticle = ParticleKind.Heart, EnterSound = "pet" },
        new() { Id = "claude-celebrate", DisplayName = "Claude Opened!", Animation = AnimationState.Celebrate, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 3f, MaxDuration = 3f, Cooldown = 0f, Mood = Mood.Excited, MoodIntensity = 1f, EnterParticle = ParticleKind.Confetti, EnterSound = "celebrate" },
        new() { Id = "wave-goodbye", DisplayName = "Wave Goodbye", Animation = AnimationState.Wave, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 2f, MaxDuration = 2f, Cooldown = 0f, Mood = Mood.Sad, MoodIntensity = 0.6f },
        new() { Id = "surprised", DisplayName = "Surprised", Animation = AnimationState.Surprised, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 0.9f, MaxDuration = 0.9f, Cooldown = 0f, Mood = Mood.Surprised, MoodIntensity = 1f },
        new() { Id = "scared", DisplayName = "Scared", Animation = AnimationState.Scared, Movement = BehaviorMovement.FleeCursor, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 1.6f, MaxDuration = 2.2f, Cooldown = 0f, Mood = Mood.Scared, MoodIntensity = 1f },
        new() { Id = "wake", DisplayName = "Wake Up", Animation = AnimationState.WakeUp, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 1.2f, MaxDuration = 1.2f, Cooldown = 0f, Mood = Mood.Surprised },
        new() { Id = "greet", DisplayName = "Hello!", Animation = AnimationState.Wave, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 2.2f, MaxDuration = 2.2f, Cooldown = 0f, Mood = Mood.Happy, MoodIntensity = 0.8f, EnterParticle = ParticleKind.Sparkle, EnterSound = "happy" },
        new() { Id = "type-along", DisplayName = "Type Along", Animation = AnimationState.TypeAlong, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 1.5f, MaxDuration = 1.5f, Cooldown = 0f, Mood = Mood.Excited, MoodIntensity = 0.5f },

        // ---- Weather reactions (weight 0: triggered by WorldDataService) ------
        new() { Id = "shiver", DisplayName = "Brrr, It's Cold", Animation = AnimationState.Shiver, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 4f, MaxDuration = 6f, Cooldown = 0f, Mood = Mood.Sad, MoodIntensity = 0.5f },
        new() { Id = "too-hot", DisplayName = "Too Hot", Animation = AnimationState.Hot, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 4f, MaxDuration = 6f, Cooldown = 0f, Mood = Mood.Lazy, MoodIntensity = 0.6f },
        new() { Id = "rainy", DisplayName = "Rainy Day", Animation = AnimationState.Idle, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 5f, MaxDuration = 7f, Cooldown = 0f, Mood = Mood.Content, EnterParticle = ParticleKind.Snow },

        // ---- Drag reactions (weight 0: triggered by the interaction layer) ----
        // After a hard spin or a heavy collision the crab gets woozy: spiral eyes, a
        // lolling head and a stumble that eases out as the dizziness meter recovers.
        new() { Id = "dizzy", DisplayName = "Dizzy", Animation = AnimationState.Dizzy, Category = BehaviorCategory.Special, Weight = 0f, MinDuration = 2.4f, MaxDuration = 3.4f, Cooldown = 0f, Mood = Mood.Confused, MoodIntensity = 0.8f, EnterParticle = ParticleKind.Star, EnterSound = "yawn" },
    ];
}
