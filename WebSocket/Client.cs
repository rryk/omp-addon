using System;
using KIARA;
using System.Collections.Generic;
using log4net;
using System.Reflection;
using OpenSim.Framework;
using OpenMetaverse.Packets;
using OpenMetaverse;
using System.Net;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OMP.WebSocket
{
    public class Client {
        #region Public interface
        public Client(Connection connection, Server server, Scene scene) {
            m_Connection = connection;
            m_Server = server;
            m_Scene = scene;

            ConfigureInterfaces();
        }
        #endregion

        #region Private implementation
        private Connection m_Connection;
        private Server m_Server;
        private Scene m_Scene;
        private Dictionary<string, FunctionWrapper> m_Functions = 
            new Dictionary<string, FunctionWrapper>();
        private static readonly ILog m_Log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<string> m_SupportedInterfaces = new List<string>();

        private FunctionCall Call(string name, params object[] parameters)
        {
            if (m_Functions.ContainsKey(name)) {
                return m_Functions[name](parameters);
            } else {
                throw new Error(ErrorCode.INVALID_ARGUMENT,
                                "Function " + name + " is not registered.");
            }
        }

        private bool InterfaceImplements(string interfaceURI)
        {
            return m_SupportedInterfaces.Contains(interfaceURI);
        }

        private void ConfigureInterfaces()
        {
            // Prepare configuration data.
            string[] localInterfaces = {
                "http://yellow.cg.uni-saarland.de/home/kiara/idl/interface.kiara",
                "http://yellow.cg.uni-saarland.de/home/kiara/idl/connectServer.kiara"
            };

            Dictionary<string, Delegate> localFunctions = new Dictionary<string, Delegate>
            {
                {"omp.interface.implements", (Func<string ,bool>)InterfaceImplements},
            };

            string[] remoteInterfaces = 
            { 
                "http://yellow.cg.uni-saarland.de/home/kiara/idl/interface.kiara",
                "http://yellow.cg.uni-saarland.de/home/kiara/idl/connectClient.kiara",
            };

            string[] remoteFunctions = 
            { 
                "omp.connect.regionHandshake",
            };

            // Set up server interfaces.
            foreach (string supportedInterface in localInterfaces) {
                m_SupportedInterfaces.Add(supportedInterface);
                m_Connection.LoadIDL(supportedInterface);
            }

            // Set up server functions.
            foreach (KeyValuePair<string, Delegate> localFunction in localFunctions) {
                m_Connection.RegisterFuncImplementation(localFunction.Key, "...",
                                                        localFunction.Value);
            }
            
            // Set up client interfaces.
            // TODO(rryk): Not sure if callbacks may be executed in several threads at the same 
            // time - perhaps we need a mutex for accessing loadedInterfaces and failedToLoad.
            int numInterfaces = remoteInterfaces.Length;
            int loadedInterfaces = 0;
            bool failedToLoad = false;

            Action<string, string> errorCallback = delegate(string interfaceName, string reason) {
                if (failedToLoad)
                    return;
                failedToLoad = true;
                m_Server.RemoveClient(this);
                m_Log.Error("Failed to acquire '" + interfaceName + "' interface - " + reason);
            };

            Action<string, Exception> excCallback = delegate(string interfaceName, 
                                                             Exception exception) {
                errorCallback(interfaceName, "exception returned by the client");
            };

            Action<string, bool> resultCallback = delegate(string interfaceName, bool result) {
                if (failedToLoad)
                    return;

                if (!result) 
                    errorCallback(interfaceName, "not supported by the client");
                else {
                    loadedInterfaces += 1;
                    if (loadedInterfaces == numInterfaces) {
                        // Set up client functions.
                        foreach (string func in remoteFunctions)
                            m_Functions[func] = m_Connection.GenerateFunctionWrapper(func, "...");
                        RegisterEvents();
                    }
                }
            };

            FunctionWrapper implements = 
                m_Connection.GenerateFunctionWrapper("omp.interface.implements", "...");
            foreach (string interfaceName in remoteInterfaces) {
                implements(interfaceName)
                    .On("error", 
                        (CallErrorCallback)((reason) => errorCallback(interfaceName, reason)))
                    .On("result", (Action<bool>)((result) => resultCallback(interfaceName, result)))
                    .On("exception", (Action<Exception>)((exc) => excCallback(interfaceName, exc)));
            }
        }

        void RegisterEvents()
        {
            m_Scene.
        }

        public void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            RegionHandshakePacket handshake = new RegionHandshakePacket();
            handshake.RegionInfo = new RegionHandshakePacket.RegionInfoBlock();
            handshake.RegionInfo.BillableFactor = args.billableFactor;
            handshake.RegionInfo.IsEstateManager = args.isEstateManager;
            handshake.RegionInfo.TerrainHeightRange00 = args.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = args.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = args.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = args.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = args.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = args.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = args.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = args.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = args.simAccess;
            handshake.RegionInfo.WaterHeight = args.waterHeight;

            handshake.RegionInfo.RegionFlags = args.regionFlags;
            handshake.RegionInfo.SimName = Util.StringToBytes256(args.regionName);
            handshake.RegionInfo.SimOwner = args.SimOwner;
            handshake.RegionInfo.TerrainBase0 = args.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = args.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = args.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = args.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = args.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = args.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = args.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = args.terrainDetail3;
            // I guess this is for the client to remember an old setting?
            handshake.RegionInfo.CacheID = UUID.Random();
            handshake.RegionInfo2 = new RegionHandshakePacket.RegionInfo2Block();
            handshake.RegionInfo2.RegionID = regionInfo.RegionID;

            handshake.RegionInfo3 = new RegionHandshakePacket.RegionInfo3Block();
            handshake.RegionInfo3.CPUClassID = 9;
            handshake.RegionInfo3.CPURatio = 1;

            handshake.RegionInfo3.ColoName = Utils.EmptyBytes;
            handshake.RegionInfo3.ProductName = Util.StringToBytes256(regionInfo.RegionType);
            handshake.RegionInfo3.ProductSKU = Utils.EmptyBytes;
            handshake.RegionInfo4 = new RegionHandshakePacket.RegionInfo4Block[0];

            Call("omp.connect.regionHandshake", handshake);
        }

        #endregion
    }
}

