using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Timers;
using F12025Telemetry;
using Timer = System.Timers.Timer;

namespace F12020Telemetry
{
    /// <summary>
    /// Client receiving telemetry updates from the F1 2020 or F1 2025 game.
    /// </summary>
    public class F12020TelemetryClient
    {
        /// <summary>
        /// packetFormat of the most recently received packet (the game year, e.g. 2020 or 2025),
        /// or null while no telemetry is being received. Parsing auto-detects the packet layout
        /// from this field, so the game never has to be selected manually.
        /// </summary>
        public ushort? DetectedPacketFormat { get; private set; }

        /// <summary>
        /// Raised whenever DetectedPacketFormat changes (new game detected or telemetry timed out).
        /// </summary>
        public event Action<ushort?> OnDetectedPacketFormatChanged;

        /// <summary>
        /// Time required to time out in MS.
        /// </summary>
        private const float TIMEOUT_IN_MS = 500.0f;

        private UdpClient _client;
        private IPEndPoint _endPoint;

        private Timer _timeoutTimer;

        /// <summary>
        /// Indicates if we are currently connected.
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// Connection status change delegate.
        /// </summary>
        /// <param name="connected">True if connected, false if disconnected.</param>
        public delegate void ConnectStatusChangeDelegate(bool connected);

        /// <summary>
        /// Called when connect status changed. 
        /// Connected bool indicates if there is a connection (true) or disconnection (false).
        /// </summary>
        public event ConnectStatusChangeDelegate OnConnectStatusChanged;

        // Delegates
        public delegate void MotionDataReceiveDelegate(PacketMotionData packet);
        public delegate void SessionDataReceiveDelegate(PacketSessionData packet);
        public delegate void LapDataReceiveDelegate(PacketLapData packet);
        public delegate void EventDataReceiveDelegate(PacketEventData packet);
        public delegate void ParticipantsDataReceiveDelegate(PacketParticipantsData packet);
        public delegate void CarSetupsDataReceiveDelegate(PacketCarSetupData packet);
        public delegate void CarTelemetryDataReceiveDelegate(PacketCarTelemetryData packet);
        public delegate void CarStatusDataReceiveDelegate(PacketCarStatusData packet);
        public delegate void FinalClassificationDataReceiveDelegate(PacketFinalClassificationData packet);
        public delegate void LobbyInfoDataReceiveDelegate(PacketLobbyInfoData packet);

        // Packet events
        public event MotionDataReceiveDelegate OnMotionDataReceive;
        public event SessionDataReceiveDelegate OnSessionDataReceive;
        public event LapDataReceiveDelegate OnLapDataReceive;
        public event EventDataReceiveDelegate OnEventDataReceive;
        public event ParticipantsDataReceiveDelegate OnParticipantsDataReceive;
        public event CarSetupsDataReceiveDelegate OnCarSetupsDataReceive;
        public event CarTelemetryDataReceiveDelegate OnCarTelemetryDataReceive;
        public event CarStatusDataReceiveDelegate OnCarStatusDataReceive;
        public event FinalClassificationDataReceiveDelegate OnFinalClassificationDataReceive;
        public event LobbyInfoDataReceiveDelegate OnLobbyInfoDataReceive;

        /// <summary>
        /// Constructs telemetry client and sets it up for receiving data.
        /// </summary>
        /// <param name="port">The port to listen to. This should match the game setting.</param>
        public F12020TelemetryClient(int port)
        {
            _client = new UdpClient(port);
            _endPoint = new IPEndPoint(IPAddress.Loopback, port);

            _timeoutTimer = new Timer(TIMEOUT_IN_MS);
            _timeoutTimer.AutoReset = false;
            _timeoutTimer.Elapsed += TimeoutEvent;

            // Start receiving updates.
            _client.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }

        /// <summary>
        /// Called when data is received.
        /// </summary>
        /// <param name="result">Resulting data.</param>
        private void ReceiveCallback(IAsyncResult result)
        {
            // Handle connected event.
            if (Connected == false)
            {
                Connected = true;
                OnConnectStatusChanged?.Invoke(true);
            }

            // Restart the timeout timer.
            _timeoutTimer.Stop();
            _timeoutTimer.Start();

            // Get data we received.
            byte[] data = _client.EndReceive(result, ref _endPoint);

            // Start receiving again.
            _client.BeginReceive(new AsyncCallback(ReceiveCallback), null);

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                // The packet format is the first ushort of every header in all game versions,
                // so the game can be auto-detected per packet: F1 23 and later use the
                // 29-byte header, F1 2020-2022 the 24-byte one.
                if (data.Length < 2)
                    return;
                ushort packetFormat = BitConverter.ToUInt16(data, 0);

                if (packetFormat >= 2020 && DetectedPacketFormat != packetFormat)
                {
                    DetectedPacketFormat = packetFormat;
                    OnDetectedPacketFormatChanged?.Invoke(packetFormat);
                }

                if (packetFormat >= 2023)
                {
                    HandleF12025Packet(data, handle, packetFormat);
                    return;
                }

                if (packetFormat < 2020)
                    return;

                // Get the header to retrieve the packet ID.
                PacketHeader header = (PacketHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketHeader));

                // The motion packet (the only one SpeedSeat consumes) is byte-identical in
                // F1 2020-2022, but the other packet layouts changed between those games.
                if (packetFormat != 2020 && header.packetId != PacketID.MOTION)
                    return;

                // Switch on packet id, and call the correct event.
                // Cast the packet to the correct type based on the ID.
                switch (header.packetId)
                {
                    case PacketID.MOTION:
                        PacketMotionData motionData = (PacketMotionData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketMotionData));
                        OnMotionDataReceive?.Invoke(motionData);
                        break;
                    case PacketID.SESSION:
                        PacketSessionData sessionData = (PacketSessionData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketSessionData));
                        OnSessionDataReceive?.Invoke(sessionData);
                        break;
                    case PacketID.LAP_DATA:
                        PacketLapData lapData = (PacketLapData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketLapData));
                        OnLapDataReceive?.Invoke(lapData);
                        break;
                    case PacketID.EVENT:
                        PacketEventData eventData = (PacketEventData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketEventData));
                        OnEventDataReceive?.Invoke(eventData);
                        break;
                    case PacketID.PARTICIPANTS:
                        PacketParticipantsData participantsData = (PacketParticipantsData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketParticipantsData));
                        OnParticipantsDataReceive?.Invoke(participantsData);
                        break;
                    case PacketID.CAR_SETUPS:
                        PacketCarSetupData carSetupsData = (PacketCarSetupData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketCarSetupData));
                        OnCarSetupsDataReceive?.Invoke(carSetupsData);
                        break;
                    case PacketID.CAR_TELEMETRY:
                        PacketCarTelemetryData telemetryData = (PacketCarTelemetryData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketCarTelemetryData));
                        OnCarTelemetryDataReceive?.Invoke(telemetryData);
                        break;
                    case PacketID.CAR_STATUS:
                        PacketCarStatusData carStatusData = (PacketCarStatusData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketCarStatusData));
                        OnCarStatusDataReceive?.Invoke(carStatusData);
                        break;
                    case PacketID.FINAL_CLASSIFICATION:
                        PacketFinalClassificationData finalClassificationData = (PacketFinalClassificationData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketFinalClassificationData));
                        OnFinalClassificationDataReceive?.Invoke(finalClassificationData);
                        break;
                    case PacketID.LOBBY_INFO:
                        PacketLobbyInfoData lobbyInfoData = (PacketLobbyInfoData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketLobbyInfoData));
                        OnLobbyInfoDataReceive?.Invoke(lobbyInfoData);
                        break;
                }
            }
            catch
            {
                Console.WriteLine("Failed to receive F1 telemetry packet.");
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Parses a packet using the F1 25 layout (header shared by F1 23 and later).
        /// Only the motion packet is needed for seat control.
        /// </summary>
        private void HandleF12025Packet(byte[] data, GCHandle handle, ushort packetFormat)
        {
            // The 29-byte header layout was introduced with F1 23; reject older formats.
            if (packetFormat < 2023 || data.Length < Marshal.SizeOf(typeof(PacketHeader2025)))
                return;

            PacketHeader2025 header = (PacketHeader2025)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketHeader2025));

            if (header.packetId != PacketID.MOTION || data.Length < Marshal.SizeOf(typeof(PacketMotionData2025)))
                return;

            PacketMotionData2025 motionData = (PacketMotionData2025)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketMotionData2025));
            OnMotionDataReceive?.Invoke(motionData.ToPacketMotionData());
        }

        /// <summary>
        /// Called when no data is received for a period of time.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Elapsed event arguments.</param>
        private void TimeoutEvent(object sender, ElapsedEventArgs e)
        {
            Connected = false;
            OnConnectStatusChanged?.Invoke(false);
            if (DetectedPacketFormat != null)
            {
                DetectedPacketFormat = null;
                OnDetectedPacketFormatChanged?.Invoke(null);
            }
        }
    }
}
