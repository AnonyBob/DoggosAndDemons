using FirstGearGames.Utilities.Networks;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace FirstGearGames.Mirrors.Assets.FlexNetworkAnimators
{

    [System.Serializable]
    public struct AnimatorUpdateMessage : NetworkMessage
    {
        public ArraySegment<byte> Data;
    }

    public class FlexNetworkAnimatorManager : MonoBehaviour
    {
        #region Serialized
        private NetworkManager _networkManager = null;
        /// <summary>
        /// Current NetworkManager.
        /// </summary>
        public NetworkManager CurrentNetworkManager { get { return _networkManager; } }
        /// <summary>
        /// True to make this gameObject dont destroy on load. True is recommended if your NetworkManager is also dont destroy on load.
        /// </summary>
        private bool _dontDestroyOnLoad = true;
        #endregion

        #region Private.
        /// <summary>
        /// Active FlexNetworkTransform components.
        /// </summary>
        private static List<FlexNetworkAnimator> _activeFlexNetworkAnimators = new List<FlexNetworkAnimator>();
        /// <summary>
        /// Reliable datas to send to all.
        /// </summary>
        private static List<ArraySegment<byte>> _toAllReliableAnimatorUpdate = new List<ArraySegment<byte>>();
        /// <summary>
        /// Reliable datas to send send to server.
        /// </summary>
        private static List<ArraySegment<byte>> _toServerReliableAnimatorUpdate = new List<ArraySegment<byte>>();
        /// <summary>
        /// Reliable datas sent to specific observers.
        /// </summary>
        private static Dictionary<NetworkConnection, List<ArraySegment<byte>>> _observerReliableAnimatorUpdate = new Dictionary<NetworkConnection, List<ArraySegment<byte>>>();
        /// <summary>
        /// Last NetworkClient.active state.
        /// </summary>
        private bool _lastClientActive = false;
        /// <summary>
        /// Last NetworkServer.active state.
        /// </summary>
        private bool _lastServerActive = false;
        /// <summary>
        /// How much data can be bundled per reliable message.
        /// </summary>
        private int _reliableMTU = -1;
        /// <summary>
        /// How much data can be bundled per unreliable message.
        /// </summary>
        private int _unreliableMTU = -1;
        /// <summary>
        /// Used to prevent GC with GetComponents.
        /// </summary>
        private List<FlexNetworkAnimator> _getComponents = new List<FlexNetworkAnimator>();
        /// <summary>
        /// Singleton of this script. Used to ensure script is not loaded more than once. This will change for NG once custom message subscriptions are supported.
        /// </summary>
        private static FlexNetworkAnimatorManager _instance;
        /// <summary>
        /// Buffer to send outgoing data. Segments will always be 1200 or less.
        /// </summary>
        private byte[] _writerBuffer = new byte[1200];
        #endregion

        #region Const.
        /// <summary>
        /// Maximum packet size by default. This is used when packet size is unknown.
        /// </summary>
        private const int MAXIMUM_PACKET_SIZE = 1200;
        /// <summary>
        /// Guestimated amount of how much MTU will be needed to send one transform on any transport. This will likely never be a problem but just incase.
        /// </summary>
        private const int MINIMUM_MTU_REQUIREMENT = 150;
        #endregion

#if MIRROR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void MirrorFirstInitialize()
        {
            GameObject go = new GameObject();
            go.name = "FlexNetworkAnimatorManager";
            go.AddComponent<FlexNetworkAnimatorManager>();
        }
#endif

        private void Awake()
        {
            FirstInitialize();
        }

        /// <summary>
        /// Initializes script for use. Should only be completed once.
        /// </summary>
        private void FirstInitialize()
        {
            if (_instance != null)
            {
                Debug.LogError("Multiple FlexNetworkAnimatorManager instances found. This new instance will be destroyed.");
                Destroy(this);
                return;
            }

            _instance = this;
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }


        private void Update()
        {
#if MIRROR
            CheckRegisterHandlers();
#endif
            //Run updates on FlexNetworkTransforms.
            for (int i = 0; i < _activeFlexNetworkAnimators.Count; i++)
                _activeFlexNetworkAnimators[i].ManualUpdate();

            //Send any queued messages.
            SendMessages();
        }

        /// <summary>
        /// Registers handlers for the client.
        /// </summary>
        private void CheckRegisterHandlers()
        {
            bool ncActive = Platforms.ReturnClientActive(CurrentNetworkManager);
            bool nsActive = Platforms.ReturnServerActive(CurrentNetworkManager);
            bool changed = (_lastClientActive != ncActive || _lastServerActive != nsActive);
            //If wasn't active previously but is now then get handlers again.
            if (changed && ncActive)
                NetworkReplaceHandlers(true);
            if (changed && nsActive)
                NetworkReplaceHandlers(false);

            _lastClientActive = ncActive;
            _lastServerActive = nsActive;
        }

        /// <summary>
        /// Adds to ActiveFlexNetworkTransforms.
        /// </summary>
        /// <param name="FNA"></param>
        public static void AddToActive(FlexNetworkAnimator fna)
        {
            _activeFlexNetworkAnimators.Add(fna);
        }
        /// <summary>
        /// Removes from ActiveFlexNetworkTransforms.
        /// </summary>
        /// <param name="FNA"></param>
        public static void RemoveFromActive(FlexNetworkAnimator FNA)
        {
            _activeFlexNetworkAnimators.Remove(FNA);
        }

        /// <summary>
        /// Sends data to server.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        public static void SendToServer(ArraySegment<byte> data)
        {
            _toServerReliableAnimatorUpdate.Add(data);
        }

        /// <summary>
        /// Sends data to all.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        public static void SendToAll(ArraySegment<byte> data)
        {
            _toAllReliableAnimatorUpdate.Add(data);
        }

        /// <summary>
        /// Sends data to observers.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        public static void SendToObserver(NetworkConnection conn, ArraySegment<byte> data)
        {
            List<ArraySegment<byte>> datas;
            //If doesn't have datas for connection yet then make new datas.
            if (!_observerReliableAnimatorUpdate.TryGetValue(conn, out datas))
            {
                datas = new List<ArraySegment<byte>>();
                _observerReliableAnimatorUpdate[conn] = datas;
            }

            datas.Add(data);
        }

        /// <summary>
        /// Sends queued messages.
        /// </summary>
        private void SendMessages()
        {
            //If MTUs haven't been set yet.
            if (_reliableMTU == -1 || _unreliableMTU == -1)
                Platforms.SetMTU(ref _reliableMTU, ref _unreliableMTU, MAXIMUM_PACKET_SIZE);

            //Server.
            if (Platforms.ReturnServerActive(CurrentNetworkManager))
            {
                //Reliable to all.
                SendAnimatorUpdates(false, null, _toAllReliableAnimatorUpdate, true);

                //Reliable to observers.
                foreach (KeyValuePair<NetworkConnection, List<ArraySegment<byte>>> item in _observerReliableAnimatorUpdate)
                {
                    //Null or unready network connection.
                    if (item.Key == null || !item.Key.IsReady())
                        continue;

                    SendAnimatorUpdates(false, item.Key, item.Value, true);
                }
            }
            //Client.
            if (Platforms.ReturnClientActive(CurrentNetworkManager))
            {
                //Reliable to server.
                SendAnimatorUpdates(true, null, _toServerReliableAnimatorUpdate, true);
            }

            _toServerReliableAnimatorUpdate.Clear();
            _toAllReliableAnimatorUpdate.Clear();
            _observerReliableAnimatorUpdate.Clear();
        }

        /// <summary>
        /// Sends data to all or specified connection.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="datas"></param>
        /// <param name="reliable"></param>
        private void SendAnimatorUpdates(bool toServer, NetworkConnection conn, List<ArraySegment<byte>> datas, bool reliable)
        {
            int index = 0;
            int channel = (reliable) ? 0 : 1;
            int mtu = (reliable) ? _reliableMTU : _unreliableMTU;
            mtu -= 75;
#if UNITY_EDITOR
            if (mtu < MINIMUM_MTU_REQUIREMENT)
                Debug.LogWarning("MTU is dangerously low on channel " + channel + ". Data may not send properly.");
#endif

            while (index < datas.Count)
            {
                int writerPosition = 0;
                //Write until break or all data is written.
                while (writerPosition < mtu && index < datas.Count)
                {
                    ArraySegment<byte> data = datas[index];
                    int dataCount = data.Count;
                    //If will fit into the packet.
                    if (dataCount + writerPosition <= mtu)
                    {
                        Array.Copy(data.Array, 0, _writerBuffer, writerPosition, dataCount);
                        writerPosition += dataCount;
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }

                AnimatorUpdateMessage msg = new AnimatorUpdateMessage()
                {
                    Data = new ArraySegment<byte>(_writerBuffer, 0, writerPosition)
                };

                if (toServer)
                {
                    Platforms.ClientSend(CurrentNetworkManager, msg, channel);
                }
                else
                {
                    //If no connection then send to all.
                    if (conn == null)
                        Platforms.ServerSendToAll(CurrentNetworkManager, msg, channel);
                    //Otherwise send to connection.
                    else
                        conn.Send(msg, channel);
                }
            }
        }

        /// <summary>
        /// Received on clients when server sends data.
        /// </summary>
        /// <param name="msg"></param>
        private void OnServerAnimatorUpdate(AnimatorUpdateMessage msg)
        {
            AnimatorUpdateMessageReceived(msg, true);
        }

        /// <summary>
        /// Received on server when client sends data.
        /// </summary>
        /// <param name="msg"></param>
        private void OnClientAnimatorUpdate(AnimatorUpdateMessage msg)
        {
            AnimatorUpdateMessageReceived(msg, false);
        }

        /// <summary>
        /// Called when an AnimatorUpdateMessage is received.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="receivedOnClient"></param>
        private void AnimatorUpdateMessageReceived(AnimatorUpdateMessage msg, bool receivedOnClient)
        {
            int readPosition = 0;
            while (readPosition < msg.Data.Count)
            {
                AnimatorUpdate au = new AnimatorUpdate();
                Serialization.DeserializeAnimatorUpdate(ref au, ref readPosition, msg.Data);
                /* Initially I tried caching the getcomponent calls but the performance difference
                 * couldn't be registered. At this time it's not worth creating the extra complexity
                 * for what might be a 1% fps difference. */
                if (Platforms.ReturnSpawned(CurrentNetworkManager).TryGetValue(au.NetworkIdentity, out NetworkIdentity ni))
                {
                    FlexNetworkAnimator fna = ReturnFNAOnNetworkIdentity(ni, au.ComponentIndex);
                    if (fna != null)
                    {
                        if (receivedOnClient)
                            fna.ServerDataReceived(ref au);
                        else
                            fna.ClientDataReceived(ref au);
                    }
                }
            }
        }
        /// <summary>
        /// Returns a FlexNetworkTransformBase on a networkIdentity using a componentIndex.
        /// </summary>
        /// <param name="componentIndex"></param>
        /// <returns></returns>
        private FlexNetworkAnimator ReturnFNAOnNetworkIdentity(NetworkIdentity ni, byte componentIndex)
        {
            NetworkBehaviour nb = Lookups.ReturnNetworkBehaviour(ni, componentIndex);
            if (nb == null)
                return null;

            nb.GetComponents<FlexNetworkAnimator>(_getComponents);
            /* Now find the FNA which matches the component index. There is probably only one FNA
             * but if the user could have more so it's important to get all FNA
             * on the object. */
            for (int i = 0; i < _getComponents.Count; i++)
            {
                //Match found.
                if (_getComponents[i].CachedComponentIndex == componentIndex)
                    return _getComponents[i];
            }

            /* If here then the component index was found but the FNA with the component index
             * was not. This should never happen. */
            Debug.LogWarning("ComponentIndex found but FlexNetworkAnimator was not.");
            return null;
        }

        /// <summary>
        /// Replaces handlers.
        /// </summary>
        /// <param name="client">True to replace for client.</param>
        private void NetworkReplaceHandlers(bool client)
        {
            if (client)
                NetworkClient.ReplaceHandler<AnimatorUpdateMessage>(OnServerAnimatorUpdate);
            else
                NetworkServer.ReplaceHandler<AnimatorUpdateMessage>(OnClientAnimatorUpdate);
        }

    }


}