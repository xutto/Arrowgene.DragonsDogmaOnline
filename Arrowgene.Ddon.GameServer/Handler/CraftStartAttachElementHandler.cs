using Arrowgene.Ddon.GameServer.Characters;
using Arrowgene.Ddon.Server;
using Arrowgene.Ddon.Shared.Entity.PacketStructure;
using Arrowgene.Ddon.Shared.Entity.Structure;
using Arrowgene.Ddon.Shared.Model;
using Arrowgene.Logging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Arrowgene.Ddon.GameServer.Handler
{
    public class CraftStartAttachElementHandler : GameRequestPacketHandler<C2SCraftStartAttachElementReq, S2CCraftStartAttachElementRes>
    {
        private static readonly ServerLogger Logger = LogProvider.Logger<ServerLogger>(typeof(CraftStartAttachElementHandler));

        public CraftStartAttachElementHandler(DdonGameServer server) : base(server)
        {
        }

        public override S2CCraftStartAttachElementRes Handle(GameClient client, C2SCraftStartAttachElementReq request)
        {
            S2CItemUpdateCharacterItemNtc updateCharacterItemNtc = new S2CItemUpdateCharacterItemNtc();

            var (storageType, itemProps) = client.Character.Storage.FindItemByUIdInStorage(ItemManager.EquipmentStorages, request.EquipItemUId);
            var (slotNo, item, amount) = itemProps;

            ClientItemInfo clientItemInfo = ClientItemInfo.GetInfoForItemId(Server.AssetRepository.ClientItemInfos, item.ItemId);
            var result = new S2CCraftStartAttachElementRes();

            ushort relativeSlotNo = slotNo;
            CharacterCommon characterCommon = null;
            if (storageType == StorageType.CharacterEquipment)
            {
                characterCommon = client.Character;
                result.CurrentEquip.EquipSlot.CharacterId = client.Character.CharacterId;
                result.CurrentEquip.EquipSlot.PawnId = 0;
            }
            else if (storageType == StorageType.PawnEquipment)
            {
                uint pawnId = Storages.DeterminePawnId(client.Character, storageType, relativeSlotNo);
                characterCommon = client.Character.Pawns.Where(x => x.PawnId == pawnId).SingleOrDefault();
                relativeSlotNo = EquipManager.DeterminePawnEquipSlot(relativeSlotNo);
                result.CurrentEquip.EquipSlot.CharacterId = 0;
                result.CurrentEquip.EquipSlot.PawnId = pawnId;

            }

            if (storageType == StorageType.CharacterEquipment || storageType == StorageType.PawnEquipment)
            {
                result.CurrentEquip.EquipSlot.EquipSlotNo = EquipManager.DetermineEquipSlot(relativeSlotNo);
                result.CurrentEquip.EquipSlot.EquipType = EquipManager.GetEquipTypeFromSlotNo(relativeSlotNo);
            }

            var craftInfo = Server.AssetRepository.ElementAttachInfoAsset.ElementAttachInfo[clientItemInfo.Rank];
            uint totalCost = (uint)(craftInfo.Cost * request.CraftElementList.Count);
            uint totalExp = (uint)(craftInfo.Exp * request.CraftElementList.Count);

            updateCharacterItemNtc.UpdateItemList.Add(Server.ItemManager.CreateItemUpdateResult(characterCommon, item, storageType, relativeSlotNo, 0, 0));
            foreach (var element in request.CraftElementList)
            {
                uint crestId = Server.ItemManager.LookupItemByUID(Server, element.ItemUId);

                Server.Database.InsertCrest(client.Character.CommonId, request.EquipItemUId, element.SlotNo, crestId, 0);
                result.EquipElementParamList.Add(new CDataEquipElementParam()
                {
                    CrestId = crestId,
                    SlotNo = element.SlotNo,
                });

                item.EquipElementParamList.Add(new CDataEquipElementParam()
                {
                    CrestId = crestId,
                    SlotNo = element.SlotNo,
                });

                // Consume the crest
                updateCharacterItemNtc.UpdateItemList.AddRange(Server.ItemManager.ConsumeItemByUIdFromMultipleStorages(Server, client.Character, ItemManager.BothStorageTypes, element.ItemUId, 1));
            }
            
            updateCharacterItemNtc.UpdateType = ItemNoticeType.StartAttachElement;
            updateCharacterItemNtc.UpdateWalletList.Add(Server.WalletManager.RemoveFromWallet(client.Character, WalletType.Gold, totalCost));
            updateCharacterItemNtc.UpdateItemList.Add(Server.ItemManager.CreateItemUpdateResult(characterCommon, item, storageType, relativeSlotNo, 1, 1));
            client.Send(updateCharacterItemNtc);

            // TODO: Store saved pawn exp
            S2CCraftCraftExpUpNtc expNtc = new S2CCraftCraftExpUpNtc()
            {
                PawnId = request.CraftMainPawnId,
                AddExp = totalExp,
                ExtraBonusExp = 0,
                TotalExp = totalExp,
                CraftRankLimit = 0
            };
            client.Send(expNtc);

            return result;
        }
    }
}
