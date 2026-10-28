using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Item
{
    public class ClientItemUseHandler : IMessageHandler<IWorldSession, ClientItemUse>
    {
        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;
        private readonly IPrerequisiteManager prerequisiteManager;

        public ClientItemUseHandler(
            IGameTableManager gameTableManager,
            IPrerequisiteManager prerequisiteManager)
        {
            this.gameTableManager    = gameTableManager;
            this.prerequisiteManager = prerequisiteManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientItemUse itemUse)
        {
            IItem item = session.Player.Inventory.GetItem(itemUse.Location);
            if (item == null)
                throw new InvalidPacketValueException();

            ItemSpecialEntry itemSpecial = gameTableManager.ItemSpecial.GetEntry(item.Info.Entry.ItemSpecialId00);
            if (itemSpecial == null)
                throw new InvalidPacketValueException();

            if (itemSpecial.Spell4IdOnActivate > 0u)
            {
                if (itemSpecial.PrerequisiteIdGeneric00 > 0 && !prerequisiteManager.Meets(session.Player, itemSpecial.PrerequisiteIdGeneric00))
                {
                    session.Player.SendGenericError(GenericError.UnlockItemFailed); // TODO: Confirm right error message.
                    return;
                }

                // the item is consumed before the cast is attempted, so a cast the spell's own caster
                // cast prerequisite refuses would eat it. This is the gate the client evaluates
                // locally before it sends the use at all; for an AMP Power Upgrade item it is
                // Prerequisite 28557, "bonus power < 10". Only checked when no runner prerequisite
                // could override it, so it can never refuse a cast Spell.Cast would have allowed.
                Spell4Entry spell4Entry = gameTableManager.Spell4.GetEntry(itemSpecial.Spell4IdOnActivate);
                if (spell4Entry != null
                    && spell4Entry.PrerequisiteIdCasterCast > 0u
                    && spell4Entry.PrerequisiteIdRunners.All(p => p == 0u)
                    && !prerequisiteManager.Meets(session.Player, spell4Entry.PrerequisiteIdCasterCast))
                {
                    session.Player.SendGenericError(GenericError.UnlockItemFailed); // TODO: Confirm right error message.
                    return;
                }

                if (session.Player.Inventory.ItemUse(item))
                {
                    session.Player.CastSpell(itemSpecial.Spell4IdOnActivate, new SpellParameters
                    {
                        PrimaryTargetId = itemUse.TargetUnitId,
                        TargetPosition  = itemUse.Position
                    });
                }
            }
        }
    }
}
