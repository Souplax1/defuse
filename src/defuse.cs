using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives; // Vector
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace defuse;

[PluginMetadata(Id = "defuse", Version = "1.1.0", Name = "InstaDefuse", Author = "yeezy", Description = "Conditional fast defuse - blocks if molly near or T alive")]
public partial class defuse : BasePlugin
{
    private Vector? _bombSitePosition = null;
    private float _bombPlantedTime = float.NaN;
    private CPlantedC4? _plantedBomb = null;

    private List<Vector> _activeInfernos = new(); // Molly fire centers
    private const float MOLLY_BLOCK_RADIUS = 120f;

    public defuse(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        // Bomb planted
        Core.GameEvent.HookPre<EventBombPlanted>(@event =>
        {
            var bomb = FindPlantedBomb();
            if (bomb == null || !bomb.IsValid) return HookResult.Continue;

            _bombSitePosition = bomb.AbsOrigin;
            _bombPlantedTime = Core.Engine.GlobalVars.CurrentTime;
            _plantedBomb = bomb;


            return HookResult.Continue;
        });

        // Track molotov/incendiary fires
        Core.GameEvent.HookPre<EventInfernoStartburn>(@event =>
        {
            Vector firePos = new Vector(@event.X, @event.Y, @event.Z);
            _activeInfernos.Add(firePos);

            return HookResult.Continue;
        });

        Core.GameEvent.HookPre<EventInfernoExpire>(@event =>
        {
            ResetAll();
            return HookResult.Continue;
        });

        // Defuse start - conditional instant
        Core.GameEvent.HookPre<EventBombBegindefuse>(@event =>
        {
            var defuser = @event.UserIdController;
            if (defuser == null || !defuser.IsValid || !defuser.PawnIsAlive) return HookResult.Continue;

            if (_plantedBomb == null || !_plantedBomb.IsValid || _plantedBomb.CannotBeDefused)
            {
                return HookResult.Continue;
            }

            if (float.IsNaN(_bombPlantedTime))
            {
                Core.PlayerManager.SendChatEOT("[Defuse] Error: No plant time.");
                return HookResult.Continue;
            }

            // Time check

            var bombTimeUntilDetonation = _plantedBomb.TimerLength - (Core.Engine.GlobalVars.CurrentTime - _bombPlantedTime);

            var defuseLength = _plantedBomb.DefuseLength;
            if (defuseLength != 5 && defuseLength != 10)
            {
                defuseLength = defuser.PawnHasDefuser ? 5.0f : 10.0f;
            }

            var timeLeftAfterDefuse = bombTimeUntilDetonation - defuseLength;
            var bombCanBeDefusedInTime = timeLeftAfterDefuse >= 0.0f;

            // T alive check
            bool terroristsAlive = Core.PlayerManager.GetTAlive().Any();

            //Moly Near check
            bool mollyNear = false;
            foreach (var fire in _activeInfernos)
            {
                // 2D distance
                float dx = fire.X - _bombSitePosition.Value.X;
                float dy = fire.Y - _bombSitePosition.Value.Y;
                float dist2D = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist2D < MOLLY_BLOCK_RADIUS)
                {
                    mollyNear = true;
                }
            }

            Core.Scheduler.NextTick(() =>
            {
                if (_bombSitePosition != null)
                {
                    // Only instant-defuse when NO terrorists AND NO molly nearby
                    if (!terroristsAlive && !mollyNear)
                        _plantedBomb.DefuseCountDown.Value = 0;
                    
                }
            });
            return HookResult.Continue;
        });

        // Reset on round events
        Core.GameEvent.HookPre<EventRoundStart>(@event => { ResetAll(); return HookResult.Continue; });
        Core.GameEvent.HookPre<EventBombDefused>(@event => { ResetAll(); return HookResult.Continue; });
        Core.GameEvent.HookPre<EventBombExploded>(@event => { ResetAll(); return HookResult.Continue; });

    }

    public override void Unload()
    {
        Console.WriteLine("[Defuse] Unloaded");
    }

    private CPlantedC4? FindPlantedBomb()
    {
        var bombs = Core.EntitySystem.GetAllEntitiesByDesignerName<CPlantedC4>("planted_c4");
        return bombs.FirstOrDefault(b => b.IsValid);
    }

    private void ResetAll()
    {
        _bombSitePosition = null;
        _bombPlantedTime = float.NaN;
        _plantedBomb = null;
        
    }
}