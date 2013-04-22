/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using KIARA;
using log4net;
using Nini.Config;
using OpenMetaverse.Packets;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Servers;
using OpenSim.Framework;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OMP.WebSocket
{
    public sealed class Server
    {
        #region Public interface
        public Scene Scene { get { return m_Scene; } }

        // Create an instance of the server for the region represented by |scene|.
        public Server(Scene scene) {
            m_Scene = scene;
            m_CircuitManager = m_Scene.AuthenticateHandler;
            m_HttpServer = MainServer.Instance;
//            m_configSource = m_Scene.Config;
        }

        public void Start()
        {
            m_HttpServer.AddWebSocketHandler(m_RegionServicePath, HandleNewClient);
        }

        public void Stop()
        {
            m_HttpServer.RemoveWebSocketHandler(m_RegionServicePath);
        }
        #endregion

        #region Internal methods
        internal void RemoveClient(Client client)
        {
            m_Clients.Remove(client);
        }
        #endregion

        #region Private implementation
        private Scene m_Scene;
        private AgentCircuitManager m_CircuitManager;
//        private IConfigSource m_configSource = null;
        private BaseHttpServer m_HttpServer;
//        private static readonly ILog m_Log = 
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Client> m_Clients = new List<Client>();

        // Unique region service path.
        private string m_RegionServicePath {
            get { return "/region/" + Scene.RegionInfo.RegionLocX + "x" + Scene.RegionInfo.RegionLocY; }
        }

        private class WSConnectionWrapper : IWebSocketJSONConnection {
            public event ConnectionMessageDelegate OnMessage;
            public event ConnectionCloseDelegate OnClose;
            public event ConnectionErrorDelegate OnError;

            public WSConnectionWrapper(WebSocketHttpServerHandler handler)
            {
                m_handler = handler;
            }

            public bool Send(string data)
            {
              m_handler.SendMessage(data);
              return true;
            }

            public void Listen()
            {
              m_handler.OnText += (sender, text) => OnMessage(text.Data);
              m_handler.OnClose += (sender, data) => OnClose();
              m_handler.OnUpgradeFailed += (sender, data) => OnError("Upgrade failed.");
              m_handler.HandshakeAndUpgrade();
            }

            private WebSocketHttpServerHandler m_handler;
        }

        private static bool InterfaceImplements(string interfaceURI) 
        {
            string[] supportedInterfaces = {
                "http://yellow.cg.uni-saarland.de/home/kiara/idl/interface.kiara",
                "http://yellow.cg.uni-saarland.de/home/kiara/idl/connectServer.kiara"
            };
            return Array.IndexOf(supportedInterfaces, interfaceURI) != -1;
        }

        private void ConnectUseCircuitCode(Connection conn, uint code, string agentID, 
                                           string sessionID)
        {
            AuthenticateResponse authResponse =
                m_CircuitManager.AuthenticateSession(new UUID(sessionID), new UUID(agentID), code);
            if (authResponse.Authorised) 
            {
                m_Clients.Add(new Client(conn, this, m_Scene));
            }
        }
        
        private void HandleNewClient(string servicepath, WebSocketHttpServerHandler handler) {
            Connection conn = new Connection(new WSConnectionWrapper(handler));
            conn.LoadIDL("http://yellow.cg.uni-saarland.de/home/kiara/idl/interface.kiara");
            conn.LoadIDL("http://yellow.cg.uni-saarland.de/home/kiara/idl/connectServer.kiara");
            conn.RegisterFuncImplementation("omp.interface.implements", "...",
                (Func<string, bool>)InterfaceImplements);
            conn.RegisterFuncImplementation("omp.connect.useCircuitCode", "...",
                (Action<UInt32, string, string>)((code, agentID, sessionID) => 
                  ConnectUseCircuitCode(conn, code, agentID, sessionID)));
        }

        #endregion
    }
}
