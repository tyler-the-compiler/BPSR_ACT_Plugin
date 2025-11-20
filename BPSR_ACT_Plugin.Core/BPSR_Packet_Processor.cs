using ACT_Plugin.BlueProtobuf;
using Google.Protobuf;
using PcapDotNet.Base;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static BPSR_ACT_Plugin.Core.BPSR_Enums_Constants;
using ZstdSharp;
using System.Text;


namespace BPSR_ACT_Plugin.Core
{
    class BPSR_Packet_Processor
    {
        private readonly int MIN_PACKET_SIZE = 6;
        private readonly int MAX_PACKET_SIZE = 1024 * 1024;

        public long currentUserUuid = 0;
        public long CurrentUserUID { get; set; }
        //private Decompressor decompressor;

        public Dictionary<long, Dictionary<string, string>> PlayerCache;
        public Dictionary<long, Dictionary<string, string>> EnemyCache;
        private bool inCombat;
        private long currentLevelId;
        private static readonly uint ZSTD_MAGIC = 0xFD2FB528;
        private static readonly uint SKIPPABLE_MAGIC_MIN = 0x184D2A50;
        private static readonly uint SKIPPABLE_MAGIC_MAX = 0x184D2A5F;
        public BPSR_Packet_Processor()
        {
            inCombat = false;
            //decompressor = new Decompressor();
            PlayerCache = new Dictionary<long, Dictionary<string, string>>();
            EnemyCache = new Dictionary<long, Dictionary<string, string>>();
            currentLevelId = 0;
        }


        public void ProcessPacket(byte[] packet)
        {
            try
            {
                using (var packetsReader = new BinaryReader(new MemoryStream(packet)))
                {
                    while ((packetsReader.BaseStream.Length - packetsReader.BaseStream.Position) >= MIN_PACKET_SIZE)
                    {

                        var packetSize = BinaryPrimitives.ReverseEndianness(packetsReader.ReadUInt32());
                        packetsReader.BaseStream.Seek(-4, SeekOrigin.Current);
                        if (packetSize < MIN_PACKET_SIZE || packetSize > MAX_PACKET_SIZE)
                        {
                            Debug.WriteLine("Invalid packet length, discarding corrupted buffer");
                            return;
                        }
                        if (packetsReader.BaseStream.Length - packetsReader.BaseStream.Position < MIN_PACKET_SIZE)
                        {
                            return;
                        }

                        using (var packetReader = new BinaryReader(new MemoryStream(packetsReader.ReadBytes((int)packetSize))))
                        {
                            packetReader.ReadUInt32();
                            var prShort = packetReader.ReadUInt16();
                            var packetType = BinaryPrimitives.ReverseEndianness(prShort);
                            var isZstdCompressed = (packetType & 0x8000) != 0;
                            var msgTypeId = packetType & 0x7fff;
                            switch ((MessageType)msgTypeId)
                            {
                                case MessageType.Notify:
                                    this.ProcessNotifyMessage(packetReader, isZstdCompressed);
                                    break;
                                case MessageType.Return:
                                    break;
                                case MessageType.FrameDown:
                                    //Debug.WriteLine("MessageType FrameDown");
                                    packetReader.ReadUInt32();
                                    if (packetReader.BaseStream.Position == packetReader.BaseStream.Length)
                                    {
                                        break;
                                    }
                                    var nestedPacket = packetReader.ReadBytes((int)(packetReader.BaseStream.Length - packetReader.BaseStream.Position));
                                    if (isZstdCompressed)
                                    {
                                        nestedPacket = DecompressPayloadV2(nestedPacket);
                                    }
                                    ProcessPacket(nestedPacket);
                                    break;

                                default:
                                    break;
                            }
                        }

                    }
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error while parsing packet data for player {currentUserUuid >> 16}");
            }
        }

        private void ProcessNotifyMessage(BinaryReader packetReader, bool isZstdCompressed)
        {
            var serviceUuid = BinaryPrimitives.ReverseEndianness(packetReader.ReadUInt64());
            packetReader.ReadUInt32();
            var methodId = BinaryPrimitives.ReverseEndianness(packetReader.ReadUInt32());
            if (serviceUuid != 0x0000000063335342)
            {
                Debug.WriteLine("Skipping NotifyMsg");
                return;
            }
            var msgPayload = packetReader.ReadBytes((int)(packetReader.BaseStream.Length - packetReader.BaseStream.Position));
            if (isZstdCompressed)
            {
                msgPayload = DecompressPayloadV2(msgPayload);
            }
            switch ((NotifyMethod)methodId)
            {
                case NotifyMethod.SyncNearEntities:
                    ProcessSyncNearEntities(msgPayload);
                    break;
                case NotifyMethod.SyncContainerData:
                    ProcessSyncContainerData(msgPayload);
                    break;
                case NotifyMethod.SyncContainerDirtyData:
                    ProcessSyncContainerDirtyData(msgPayload);
                    break;
                case NotifyMethod.SyncToMeDeltaInfo:
                    ProcessSyncToMeDeltaInfo(msgPayload);
                    break;
                case NotifyMethod.SyncNearDeltaInfo:
                    ProcessSyncNearDeltaInfo(msgPayload);
                    break;
                default:
                    Debug.WriteLine($"Skipping NotifyMsg with methodId: {methodId}");
                    break;
            }
        }

        private void ProcessAoiSyncDelta(AoiSyncDelta aoiSyncDelta)
        {
            var targetUuid = aoiSyncDelta.Uuid;
            if (targetUuid < 1) { return; }

            var isTargetPlayer = IsUuidPlayer(targetUuid);
            var isTargetMonster = IsUuidMonster(targetUuid);
            var targetUid = targetUuid >> 16;

            var attrCollection = aoiSyncDelta.Attrs;
            if (!attrCollection?.Attrs.IsNullOrEmpty() ?? false)
            {
                if (isTargetPlayer)
                {
                    ProcessPlayerAttrs(targetUid, attrCollection.Attrs.ToList());
                }
                else if (isTargetMonster)
                {
                    ProcessEnemyAttrs(targetUid, attrCollection.Attrs.ToList());
                }
            }

            var skillEffect = aoiSyncDelta.SkillEffects;
            if (skillEffect == null || skillEffect.Damages.IsNullOrEmpty())
            {
                return;
            }

            foreach (var syncDamageInfo in skillEffect.Damages)
            {
                var skillId = syncDamageInfo.OwnerId;
                if (skillId < 1) { continue; }

                var attackerUuid = syncDamageInfo.TopSummonerId != 0 ? syncDamageInfo.TopSummonerId : syncDamageInfo.AttackerUuid;
                if (attackerUuid < 1) { continue; }

                var isAttackerPlayer = IsUuidPlayer(attackerUuid);
                var attackerUid = attackerUuid >> 16;

                var value = syncDamageInfo.Value;
                var luckyValue = syncDamageInfo.LuckyValue;
                var damage = value > 0 ? value : luckyValue > 0 ? luckyValue : 0;
                if (damage < 1) { continue; }

                var isCrit = (syncDamageInfo.TypeFlag & 1) == 1;
                var isLucky = luckyValue > 0;
                var isHeal = (EDamageType)syncDamageInfo.Type == EDamageType.Heal;
                var isDead = syncDamageInfo.IsDead;
                var hpLessenValue = syncDamageInfo.HpLessenValue;
                var damageElement = GetDamageElement((EDamageProperty)syncDamageInfo.Property);
                var damageSource = syncDamageInfo.DamageSource;
                var isAttackerCurrentUser = CurrentUserUID > 0 && attackerUid == CurrentUserUID;
                var isTargetCurrentUser = CurrentUserUID > 0 && targetUid == CurrentUserUID;

                if (isTargetPlayer)
                {
                    var targetPlayerName = this.GetPlayerAttribute(targetUid, "name");
                    var actionTarget = $"{targetPlayerName ?? ""}#{targetUid}";
                    var targetPlayerProfession = this.GetPlayerAttribute(targetUid, "profession") ?? "-1";
                    var targetPlayerFightPoint = this.GetPlayerAttribute(targetUid, "fight_point") ?? "-1";

                    if (isHeal) //player heals another player
                    {
                        var srcPlayerName = this.GetPlayerAttribute(attackerUid, "name");
                        var actionSource = $"{srcPlayerName ?? ""}#{attackerUid}";
                        var sourcePlayerProfession = this.GetPlayerAttribute(attackerUid, "profession") ?? "-1";
                        var sourcePlayerFightPoint = this.GetPlayerAttribute(attackerUid, "fight_point") ?? "-1";

                        BPSR_Event_Logger.Instance.AddHealingLogLine(actionSource, actionTarget, skillId, damageElement, damage, isCrit, isLucky, isAttackerCurrentUser, isTargetCurrentUser, sourcePlayerProfession, targetPlayerProfession, sourcePlayerFightPoint, targetPlayerFightPoint);
                    }
                    else if (!isDead) //enemy attacks player
                    {
                        var actionSource = (!isAttackerPlayer) ? this.GetEnemyFullName(attackerUid) : $"#{attackerUid}"; //we damage ourselves
                        BPSR_Event_Logger.Instance.AddDamageLogLine(actionSource, actionTarget, skillId, damageElement, damage, hpLessenValue, isCrit, isLucky, isAttackerCurrentUser, isTargetCurrentUser, "_", targetPlayerProfession, "_", targetPlayerFightPoint);
                    }
                    else if (isDead && PlayerCache.ContainsKey(targetUid) && PlayerCache[targetUid].ContainsKey("dead") && PlayerCache[targetUid]["dead"] == "no") //enemy attacks player and player dies
                    {
                        var actionSource = (!isAttackerPlayer) ? this.GetEnemyFullName(attackerUid) : $"#{attackerUid}"; //we damage ourselves
                        BPSR_Event_Logger.Instance.AddDeathLogLine(actionSource, actionTarget, skillId, damageElement, damage, hpLessenValue, isCrit, isLucky, isAttackerCurrentUser, isTargetCurrentUser, "_", targetPlayerProfession, "_", targetPlayerFightPoint);
                        SetPlayerAttribute(targetUid, "hp", 0);
                    }

                    SetPlayerAttribute(targetUid, "dead", isDead ? "yes" : "no");

                }
                else
                {
                    if (isAttackerPlayer)
                    {
                        if (!isHeal) //player attacks something
                        {
                            var playerName = this.GetPlayerAttribute(attackerUid, "name");
                            var attackSource = $"{playerName ?? ""}#{attackerUid}";
                            var sourcePlayerProfession = this.GetPlayerAttribute(attackerUid, "profession") ?? "-1";
                            var sourcePlayerFightPoint = this.GetPlayerAttribute(attackerUid, "fight_point") ?? "-1";


                            var attackTarget = (!isTargetPlayer) ? this.GetEnemyFullName(targetUid) : $"#{targetUid}"; //we damage ourselves

                            BPSR_Event_Logger.Instance.AddDamageLogLine(attackSource, attackTarget, skillId, damageElement, damage, hpLessenValue, isCrit, isLucky, isAttackerCurrentUser, isTargetCurrentUser, sourcePlayerProfession, "_", sourcePlayerFightPoint, "_");
                        }

                        //SetPlayerAttribute(attackerUid, "dead", isDead ? "yes" : "no");
                    }
                }
            }
        }

        private void ProcessSyncNearEntities(byte[] payloadBuffer)
        {
            var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payloadBuffer);

            if (syncNearEntities.Appear.Count == 0)
            {
                return;
            }

            foreach (var entity in syncNearEntities.Appear)
            {
                var entityUuid = entity.Uuid;
                if (entityUuid < 1) { continue; }
                var entityUid = entityUuid >> 16;
                var attrCollection = entity.Attrs;
                if (attrCollection.Attrs.Count > 0)
                {
                    switch (entity.EntType)
                    {
                        case EEntityType.EntMonster:
                            ProcessEnemyAttrs(entityUid, attrCollection.Attrs.ToList());
                            break;
                        case EEntityType.EntChar:
                            ProcessPlayerAttrs(entityUid, attrCollection.Attrs.ToList());
                            break;
                    }
                }
            }
        }

        private void ProcessSyncContainerData(byte[] payloadBuffer)
        {
            var syncContainerData = SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData.VData != null)
            {
                var sceneData = syncContainerData.VData.SceneData;
                if (sceneData.LevelMapId != currentLevelId)
                {
                    //var currentAreaId = sceneData.LevelAreaId;
                    currentLevelId = sceneData.LevelMapId;
                    BPSR_Event_Logger.Instance.AddZoneChangeLogLine(currentLevelId);
                }
            }
            else { return; }

            var vData = syncContainerData.VData;
            if (vData.CharId < 1) { return; }
            var playerUid = vData.CharId;

            var charBase = vData.CharBase;
            if (charBase == null) { return; }

            if (!String.IsNullOrEmpty(charBase.Name))
            {
                SetPlayerAttribute(playerUid, "name", charBase.Name);
            }
            if (charBase.FightPoint > 0)
            {
                SetPlayerAttribute(playerUid, "fight_point", charBase.FightPoint);
            }
            if (vData.ProfessionList != null && vData.ProfessionList.CurProfessionId > 0)
            {
                SetPlayerAttribute(playerUid, "profession", vData.ProfessionList.CurProfessionId);
            }


        }

        private void ProcessSyncContainerDirtyData(byte[] payloadBuffer)
        {
            var syncContainerDirtyData = SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerDirtyData.VData.Buffer.IsNullOrEmpty()) { return; }

            //uhh I think this means that when there is a userfightattr, we are in combat and when there isn't, we are not?
            //and the sequence we are looking for in here is [16, 0, 0, 0], which equates to tag 16 in the protobuf data, which is
            //the userfight attr within vdata

            try
            {
                var buf = syncContainerDirtyData.VData.Buffer.ToByteArray();
                using (var reader = new BinaryReader(new MemoryStream(buf)))
                {
                    if (!DoesStreamHaveIdentifier(reader)) { return; }
                    var fieldId = reader.ReadUInt32();
                    _ = reader.ReadUInt32();
                    var playerUid = CurrentUserUID;
                    switch (fieldId)
                    {

                        case 2:
                            if (!DoesStreamHaveIdentifier(reader)) { break; }
                            fieldId = reader.ReadUInt32();
                            _ = reader.ReadInt32();
                            switch (fieldId)
                            {
                                case 5:
                                    var playerName = StreamReadString(reader);
                                    if (!string.IsNullOrEmpty(playerName))
                                    {
                                        SetPlayerAttribute(playerUid, "name", playerName);
                                    }
                                    break;
                                case 35:
                                    var fightPoint = (int)reader.ReadUInt32();
                                    _ = reader.ReadInt32();
                                    if (fightPoint != 0)
                                    {
                                        SetPlayerAttribute(playerUid, "fight_point", fightPoint);
                                    }
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case 3:
                            if (!DoesStreamHaveIdentifier(reader)) { break; }
                            fieldId = reader.ReadUInt32();
                            _ = reader.ReadInt32();
                            switch (fieldId)
                            {
                                case 6:
                                    var levelMapId = reader.ReadUInt32();
                                    _ = reader.ReadInt32();
                                    if (currentLevelId != levelMapId)
                                    {
                                        currentLevelId = levelMapId;
                                        BPSR_Event_Logger.Instance.AddZoneChangeLogLine(currentLevelId, true);
                                    }
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case 6:
                            if (!DoesStreamHaveIdentifier(reader)) { break; }
                            fieldId = reader.ReadUInt32();
                            _ = reader.ReadInt32();
                            switch (fieldId)
                            {
                                case 2:
                                    if (!DoesStreamHaveIdentifier(reader)) { break; }
                                    fieldId = reader.ReadUInt32();
                                    _ = reader.ReadInt32();
                                    switch (fieldId)
                                    {
                                        case 2:
                                            if (!DoesStreamHaveIdentifier(reader)) { break; }
                                            fieldId = reader.ReadUInt32();
                                            _ = reader.ReadInt32();
                                            switch (fieldId)
                                            {
                                                case 1:
                                                    var buffUuid = reader.ReadInt64();
                                                    _ = reader.ReadInt32();
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 16:
                            if (!inCombat)
                            {
                                inCombat = true;
                                //add eventlogger out of combat log line
                            }
                            break;
                        case 61:
                            if (!DoesStreamHaveIdentifier(reader)) { break; }
                            fieldId = reader.ReadUInt32();
                            _ = reader.ReadInt32();
                            if (fieldId == 1)
                            {
                                var professionId = (int)reader.ReadUInt32();
                                _ = reader.ReadInt32();
                                if (professionId != 0)
                                {
                                    SetPlayerAttribute(playerUid, "profession", professionId);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {

            }


        }

        private void ProcessSyncToMeDeltaInfo(byte[] payloadBuffer)
        {
            var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;

            var uuid = aoiSyncToMeDelta.Uuid;

            if (uuid != currentUserUuid)
            {
                currentUserUuid = uuid;
                var uid = currentUserUuid >> 16;
                CurrentUserUID = uid;
                Debug.WriteLine($"Got a player UUID: {currentUserUuid}. UID: {uid}");
            }

            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;

            ProcessAoiSyncDelta(aoiSyncDelta);
        }

        private void ProcessSyncNearDeltaInfo(byte[] payloadBuffer)
        {
            var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
            if (syncNearDeltaInfo.DeltaInfos.Count == 0)
            {
                return;
            }
            foreach (var deltaInfos in syncNearDeltaInfo.DeltaInfos)
            {
                ProcessAoiSyncDelta(deltaInfos);
            }
        }

        private byte[] DecompressPayload(byte[] buffer)
        {
            return new byte[0];
            //var decompressed = decompressor.Unwrap(buffer, int.MaxValue);
            //return decompressed.ToArray();
        }

        private byte[] DecompressPayloadV2(byte[] buffer)
        {
            if (buffer.Length < 4) return new byte[0];

            var off = 0;
            while (off + 4 <= buffer.Length)
            {
                var magic = BitConverter.ToUInt32(buffer, off);
                if (magic == ZSTD_MAGIC) break;
                if (magic >= SKIPPABLE_MAGIC_MIN && magic <= SKIPPABLE_MAGIC_MAX)
                {
                    if (off + 8 > buffer.Length) throw new InvalidDataException("Incomplete skippable frame header");
                    var size = BitConverter.ToUInt32(buffer, off + 4);
                    if (off + 8 + size > buffer.Length) throw new InvalidDataException("Incomplete skippable frame data");
                    off += 8 + (int)size;
                    continue;
                }

                off++;
            }

            if (off + 4 > buffer.Length) return buffer;

            using (var input = new MemoryStream(buffer, off, buffer.Length - off, false))
            {
                using (var decoder = new DecompressionStream(input))
                {
                    using (var output = new MemoryStream())
                    {
                        const long MAX_OUT = 32L * 1024 * 1024; // 32MB limit
                        decoder.CopyTo(output, 8192);
                        if (output.Length > MAX_OUT)
                        {
                            throw new InvalidDataException("Decompressed data exceeds 32MB limit.");
                        }

                        return output.ToArray();

                    }
                }
            }
        }

        private void ProcessPlayerAttrs(long playerUid, List<Attr> attrs)
        {
            foreach (var attr in attrs)
            {
                if (attr.Id < 1 || attr.RawData.IsNullOrEmpty()) { continue; }
                var b = attr.RawData.ToByteArray();
                using (var stream = new CodedInputStream(b))
                {
                    switch ((AttrType)attr.Id)
                    {
                        case AttrType.AttrName:
                            var name = stream.ReadString();
                            SetPlayerAttribute(playerUid, "name", name);
                            break;

                        case AttrType.AttrProfessionId:
                            var professionId = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "profession", professionId);
                            break;

                        case AttrType.AttrFightPoint:
                            var fightPoint = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "fight_point", fightPoint);
                            break;

                        case AttrType.AttrLevel:
                            var level = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "level", level);
                            break;

                        case AttrType.AttrRankLevel:
                            var rank = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "rank_level", rank);
                            break;

                        case AttrType.AttrCri:
                            var cri = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "cri", cri);
                            break;

                        case AttrType.AttrLucky:
                            var lucky = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "lucky", lucky);
                            break;

                        case AttrType.AttrHp:
                            var hp = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "hp", hp);
                            break;

                        case AttrType.AttrMaxHp:
                            var maxHp = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "max_hp", maxHp);
                            break;

                        case AttrType.AttrElementFlag:
                            var elementFlag = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "element_flag", elementFlag);
                            break;

                        case AttrType.AttrEnergyFlag:
                            var energyFlag = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "energy_flag", energyFlag);
                            break;

                        case AttrType.AttrReductionLevel:
                            var reductionLevel = stream.ReadInt32();
                            SetPlayerAttribute(playerUid, "reduction_flag", reductionLevel);
                            break;

                        default:
                            break;

                    }
                }
            }
        }

        private void ProcessEnemyAttrs(long enemyUid, List<Attr> attrs)
        {
            foreach (var attr in attrs)
            {
                if (attr.Id < 1 || attr.RawData.IsNullOrEmpty()) { continue; }
                var b = attr.RawData.ToByteArray();
                using (var stream = new CodedInputStream(b))
                {
                    switch (attr.Id)
                    {
                        case (int)AttrType.AttrName:
                            var name = stream.ReadString();
                            SetEnemyAttribute(enemyUid, "name", name);
                            break;
                        case (int)AttrType.AttrId:
                            var attrId = stream.ReadInt32();
                            if (MonsterMap.ContainsKey(attrId))
                            {
                                SetEnemyAttribute(enemyUid, "name", MonsterMap[attrId]);
                            }
                            break;
                    }
                }
            }
        }

        private bool IsUuidPlayer(long Uuid)
        {
            return (Uuid & 0xffff) == 640;
        }

        private bool IsUuidMonster(long Uuid)
        {
            return (Uuid & 0xffff) == 64;
        }

        private void SetPlayerAttribute(long uid, string key, object value)
        {

            if (!PlayerCache.ContainsKey(uid))
            {
                PlayerCache.Add(uid, new Dictionary<string, string>());
            }

            if (!PlayerCache[uid].ContainsKey(key))
            {
                PlayerCache[uid].Add(key, value.ToString());
            }
            else
            {
                PlayerCache[uid][key] = value.ToString();
            }
        }

        private void SetEnemyAttribute(long uid, string key, object value)
        {

            if (!EnemyCache.ContainsKey(uid))
            {
                EnemyCache.Add(uid, new Dictionary<string, string>());
            }

            if (!EnemyCache[uid].ContainsKey(key))
            {
                EnemyCache[uid].Add(key, value.ToString());
            }
            else
            {
                EnemyCache[uid][key] = value.ToString();
            }
        }

        private string GetPlayerAttribute(long uid, string key)
        {
            if (!PlayerCache.ContainsKey(uid))
            {
                return null;
            }
            if (!PlayerCache[uid].ContainsKey(key))
            {
                return null;
            }

            return PlayerCache[uid][key];
        }

        private string GetEnemyAttribute(long uid, string key)
        {
            if (!EnemyCache.ContainsKey(uid))
            {
                return null;
            }
            if (!EnemyCache[uid].ContainsKey(key))
            {
                return null;
            }

            return EnemyCache[uid][key];
        }

        private string GetEnemyFullName(long uid)
        {
            var enemyName = this.GetEnemyAttribute(uid, "name");
            if (!String.IsNullOrEmpty(enemyName))
            {
                return $"{enemyName ?? ""}${uid}";
            }
            return $"${uid}";
        }

        private string GetDamageElement(EDamageProperty damagePropery)
        {
            switch (damagePropery)
            {
                case EDamageProperty.General:
                    return "Physical";
                case EDamageProperty.Fire:
                    return "Fire";
                case EDamageProperty.Water:
                    return "Water";
                case EDamageProperty.Lightning:
                    return "Lightning";
                case EDamageProperty.Forest:
                    return "Forest";
                case EDamageProperty.Wind:
                    return "Wind";
                case EDamageProperty.Earth:
                    return "Earth";
                case EDamageProperty.Light:
                    return "Light";
                case EDamageProperty.Dark:
                    return "Dark";
                case EDamageProperty.Count:
                    return "Almighty?";
                default:
                    return "Physical";
            }
        }

        private bool DoesStreamHaveIdentifier(BinaryReader reader)
        {
            var baseStream = reader.BaseStream;
            if (baseStream.Position + 8 > baseStream.Length) return false;

            var identifier = reader.ReadUInt32();
            var reversed = BinaryPrimitives.ReverseEndianness(identifier);
            _ = reader.ReadInt32();

            if (identifier != 0xfffffffe)
            {
                return false;
            }

            if (baseStream.Position + 8 > baseStream.Length) return false;

            _ = reader.ReadInt32();
            _ = reader.ReadInt32();

            return true;
        }

        private string StreamReadString(BinaryReader reader)
        {
            var length = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            var bytes = length > 0 ? reader.ReadBytes((int)length) : Array.Empty<byte>();

            _ = reader.ReadInt32();

            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }

    }
}
