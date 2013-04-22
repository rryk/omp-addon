using System;
using OpenSim.Region.Framework.Interfaces;
using System.Collections.Generic;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;
using Mono.Addins;

[assembly: Addin("OMP.WebService.RegionModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace OMP.WebSocket
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OMP.WebService.RegionModule")]
    class RegionModule : INonSharedRegionModule
    {
        public string Name { get { return "OMP.WebSocket.RegionModule"; } }
        public Type ReplaceableInterface { get { return null; } }

        private List<Server> m_Servers = new List<Server>();
        private IConfig m_Config;

        public bool Enabled { get; set; }
        
        public void Initialise(IConfigSource source)
        {
            m_Config = source.Configs["OMP.WebSocket.RegionModule"];
            if (m_Config != null && m_Config.Contains("Enabled"))
                Enabled = m_Config.GetBoolean("Enabled");
            else
                Enabled = false;
        }

        public void Close()
        {
            if (!Enabled)
                return;

            m_Servers.Clear();
        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            m_Servers.Add(new Server(scene));
        }

        public void RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;

            foreach (Server server in m_Servers) {
                if (server.Scene == scene) {
                    m_Servers.Remove(server);
                    return;
                }
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;
        }
    }
}
