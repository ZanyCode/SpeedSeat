using System.Runtime.InteropServices;
using F12020Telemetry;

namespace F12025Telemetry
{
    /// <summary>
    /// Supported telemetry source games.
    /// </summary>
    public enum GameVersion
    {
        F12020 = 2020,
        F12025 = 2025
    }

    /// <summary>
    /// Packet header used by F1 23 and later (incl. F1 25).
    /// Compared to F1 2020 it adds gameYear and overallFrameIdentifier (29 bytes total).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader2025
    {
        /// <summary>
        /// 2025
        /// </summary>
        public ushort packetFormat;

        /// <summary>
        /// Game year - last two digits, e.g. 25
        /// </summary>
        public byte gameYear;

        /// <summary>
        /// Game major version - "X.00"
        /// </summary>
        public byte gameMajorVersion;

        /// <summary>
        /// Game minor version - "1.XX"
        /// </summary>
        public byte gameMinorVersion;

        /// <summary>
        /// Version of this packet type, all start from 1
        /// </summary>
        public byte packetVersion;

        /// <summary>
        /// Identifier for the packet type, see <see cref="PacketID"/>
        /// </summary>
        public PacketID packetId;

        /// <summary>
        /// Unique identifier for the session
        /// </summary>
        public ulong sessionUID;

        /// <summary>
        /// Session timestamp
        /// </summary>
        public float sessionTime;

        /// <summary>
        /// Identifier for the frame the data was retrieved on
        /// </summary>
        public uint frameIdentifier;

        /// <summary>
        /// Overall identifier for the frame, doesn't go back after flashbacks
        /// </summary>
        public uint overallFrameIdentifier;

        /// <summary>
        /// Index of player's car in the array
        /// </summary>
        public byte playerCarIndex;

        /// <summary>
        /// Index of secondary player's car in the array (splitscreen)
        /// </summary>
        public byte secondaryPlayerCarIndex;
    }

    /// <summary>
    /// F1 25 motion packet. The per-car motion data layout is identical to F1 2020;
    /// the extra player-only data moved to the separate MotionEx packet, which we don't need.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketMotionData2025
    {
        /// <summary>
        /// Header
        /// </summary>
        public PacketHeader2025 header;

        /// <summary>
        /// Data for all cars on track
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarMotionData[] carMotionData;

        /// <summary>
        /// Converts to the F1 2020 motion packet shape so the rest of the
        /// pipeline (adaptor, seat logic) stays game-agnostic.
        /// </summary>
        public PacketMotionData ToPacketMotionData()
        {
            return new PacketMotionData
            {
                header = new PacketHeader
                {
                    packetFormat = header.packetFormat,
                    gameMajorVersion = header.gameMajorVersion,
                    gameMinorVersion = header.gameMinorVersion,
                    packetVersion = header.packetVersion,
                    packetId = header.packetId,
                    sessionUID = header.sessionUID,
                    sessionTime = header.sessionTime,
                    frameIdentifier = header.frameIdentifier,
                    playerCarIndex = header.playerCarIndex,
                    secondaryPlayerCarIndex = header.secondaryPlayerCarIndex
                },
                carMotionData = carMotionData
            };
        }
    }
}
