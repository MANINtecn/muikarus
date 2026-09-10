using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Networking;
using Client.Main.Controls.UI.Game;
using Client.Main.Helpers;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to NPC shops (NpcWindowResponse, StoreItemList, ItemBought, NpcItemBuyFailed, NpcItemSellResult).
    /// </summary>
    public class ShopHandler : IGamePacketHandler
    {
        private readonly ILogger<ShopHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly TargetProtocolVersion _targetVersion;

        public ShopHandler(
            ILoggerFactory loggerFactory,
            CharacterState characterState,
            NetworkManager networkManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<ShopHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        /// <summary>
        /// NpcWindowResponse: Open shop window when server responds with merchant window.
        /// </summary>
        [PacketHandler(0x30, PacketRouter.NoSubCode)]
        public Task HandleNpcWindowResponseAsync(Memory<byte> packet)
        {
            try
            {
                var resp = new NpcWindowResponse(packet);
                _logger.LogInformation("NpcWindowResponse received: Window={Window}", resp.Window);

                if (resp.Window == NpcWindowResponse.NpcWindow.Merchant || resp.Window == NpcWindowResponse.NpcWindow.Merchant1)
                {
                    _characterState.ClearShopItems();

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var shop = NpcShopControl.Instance;
                        shop.Visible = true;
                        shop.BringToFront();
                        OnScreenLogger.Log("[LOJA] Loja do comerciante aberta!", LogLevel.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing NpcWindowResponse packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// StoreItemList: Sent when opening merchant NPC. Displays items in the shop window.
        /// </summary>
        [PacketHandler(0x31, PacketRouter.NoSubCode)]
        public Task HandleStoreItemListAsync(Memory<byte> packet)
        {
            try
            {
                var span = packet.Span;
                if (span.Length < 7)
                {
                    _logger.LogWarning("StoreItemList packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var list = new StoreItemList(packet);
                byte count = list.ItemCount;
                int offset = 6;

                int dataSize = _targetVersion switch
                {
                    TargetProtocolVersion.Season6 => 12,
                    TargetProtocolVersion.Version097 => 11,
                    TargetProtocolVersion.Version075 => 7,
                    _ => 12
                };

                _logger.LogInformation("StoreItemList received: Type={Type}, Count={Count}, DataSize={Size}", list.Type, count, dataSize);

                _characterState.ClearShopItems();
                int parsedItems = 0;

                if (_targetVersion >= TargetProtocolVersion.Season6)
                {
                    int pos = offset;
                    int remaining = Math.Max(0, span.Length - offset);
                    bool fixedLength = count > 0 && remaining == count * (1 + 12);

                    for (int i = 0; i < count; i++)
                    {
                        if (pos + 1 > span.Length) break;

                        byte slot = span[pos];
                        pos += 1;

                        ReadOnlySpan<byte> itemSpan = span.Slice(pos);
                        int length = 12;

                        if (!fixedLength)
                        {
                            if (!ItemDataParser.TryGetExtendedItemLength(itemSpan, out length) || pos + length > span.Length)
                            {
                                if (pos + 12 <= span.Length) length = 12;
                                else break;
                            }
                        }

                        if (pos + length > span.Length) break;

                        var data = span.Slice(pos, length).ToArray();
                        pos += length;

                        _characterState.AddOrUpdateShopItem(slot, data);
                        parsedItems++;
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        var si = list[i, StoredItem.GetRequiredSize(dataSize)];
                        byte slot = si.ItemSlot;
                        var data = si.ItemData.Slice(0, dataSize).ToArray();
                        _characterState.AddOrUpdateShopItem(slot, data);
                        parsedItems++;
                    }
                }

                _logger.LogInformation("StoreItemList parsed: {Count} items.", parsedItems);
                _characterState.RaiseShopItemsChanged();
                OnScreenLogger.Log($"[LOJA] Carregados {parsedItems} itens no comerciante!", LogLevel.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing StoreItemList packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles 0x32 Buy Result: ItemBought or NpcItemBuyFailed.
        /// </summary>
        [PacketHandler(0x32, PacketRouter.NoSubCode)]
        public Task HandleNpcBuyResultsAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length == NpcItemBuyFailed.Length)
                {
                    _logger.LogWarning("NPC item buy failed.");
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        OnScreenLogger.Log("[LOJA] Falha na compra: Verifique o Zen ou espaco na mochila!", LogLevel.Warning);
                    });
                    return Task.CompletedTask;
                }

                var bought = new ItemBought(packet);
                byte slot = bought.InventorySlot;
                var data = bought.ItemData.ToArray();

                _characterState.AddOrUpdateInventoryItem(slot, data);
                string name = ItemDatabase.GetItemName(data) ?? "Item";
                _logger.LogInformation("Item bought to slot {Slot}: {Name}", slot, name);
                MuGame.ScheduleOnMainThread(() =>
                {
                    OnScreenLogger.Log($"[LOJA] Compra realizada: {name} colocado no inventario!", LogLevel.Information);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling buy result.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles 0x33 Sell Result: NpcItemSellResult.
        /// </summary>
        [PacketHandler(0x33, PacketRouter.NoSubCode)]
        public Task HandleNpcItemSellResultAsync(Memory<byte> packet)
        {
            try
            {
                var res = new NpcItemSellResult(packet);
                if (res.Success)
                {
                    if (_characterState.TryConsumePendingSellSlot(out byte soldSlot))
                    {
                        _characterState.RemoveInventoryItem(soldSlot);
                    }
                    else
                    {
                        _characterState.RaiseInventoryChanged();
                    }
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        OnScreenLogger.Log($"[LOJA] Item vendido com sucesso! Zen recebido.", LogLevel.Information);
                    });
                }
                else
                {
                    _characterState.RaiseInventoryChanged();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling sell result.");
            }
            return Task.CompletedTask;
        }
    }
}
