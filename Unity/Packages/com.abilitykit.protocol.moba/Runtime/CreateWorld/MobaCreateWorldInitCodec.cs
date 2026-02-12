using System;
using System.IO;
using System.Text;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    [MemoryPackable]
    internal readonly partial struct MobaCreateWorldLegacyReqPayload
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly EnterMobaGameReq Req;

        [MemoryPackConstructor]
        public MobaCreateWorldLegacyReqPayload(in EnterMobaGameReq req)
        {
            Req = req;
        }
    }

    public static class MobaCreateWorldInitCodec
    {
        private const uint Magic = 0x4D434957; // 'MCIW'
        private const int CurrentVersion = 2;

        public static byte[] Serialize(in MobaCreateWorldInitPayload payload)
        {
            var body = WireSerializer.Serialize(in payload);

            using var ms = new MemoryStream(body.Length + 8);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            bw.Write(Magic);
            bw.Write(CurrentVersion);
            bw.Write(body);
            bw.Flush();
            return ms.ToArray();
        }

        public static byte[] SerializeLegacyReq(in EnterMobaGameReq req)
        {
            var legacy = new MobaCreateWorldLegacyReqPayload(in req);
            var body = WireSerializer.Serialize(in legacy);

            using var ms = new MemoryStream(body.Length + 8);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            bw.Write(Magic);
            bw.Write(1);
            bw.Write(body);
            bw.Flush();
            return ms.ToArray();
        }

        public static bool TryDeserializeReq(byte[] bytes, out EnterMobaGameReq req)
        {
            if (bytes == null || bytes.Length < 12)
            {
                req = default;
                return false;
            }

            try
            {
                using var ms = new MemoryStream(bytes);
                using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                var magic = br.ReadUInt32();
                if (magic != Magic)
                {
                    req = default;
                    return false;
                }

                var ver = br.ReadInt32();
                var remaining = (int)(ms.Length - ms.Position);
                if (remaining <= 0)
                {
                    req = default;
                    return false;
                }

                var body = br.ReadBytes(remaining);

                if (ver == 1)
                {
                    var p1 = WireSerializer.Deserialize<MobaCreateWorldLegacyReqPayload>(body);
                    req = p1.Req;
                    return true;
                }

                if (ver == 2)
                {
                    var p2 = WireSerializer.Deserialize<MobaCreateWorldInitPayload>(body);
                    req = p2.ToEnterReq();
                    return true;
                }

                req = default;
                return false;
            }
            catch
            {
                req = default;
                return false;
            }
        }

        public static bool TryDeserialize(byte[] bytes, out MobaCreateWorldInitPayload payload)
        {
            if (bytes == null || bytes.Length < 12)
            {
                payload = default;
                return false;
            }

            try
            {
                using var ms = new MemoryStream(bytes);
                using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                var magic = br.ReadUInt32();
                if (magic != Magic)
                {
                    payload = default;
                    return false;
                }

                var ver = br.ReadInt32();
                if (ver != 2)
                {
                    payload = default;
                    return false;
                }

                var remaining = (int)(ms.Length - ms.Position);
                if (remaining <= 0)
                {
                    payload = default;
                    return false;
                }

                var body = br.ReadBytes(remaining);
                payload = WireSerializer.Deserialize<MobaCreateWorldInitPayload>(body);
                return true;
            }
            catch
            {
                payload = default;
                return false;
            }
        }
    }
}
