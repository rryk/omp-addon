using System;
using OpenSim.Region.Framework.Interfaces;
using System.Collections.Generic;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;

namespace OMP.WebSocket
{
    class RegionModule : INonSharedRegionModule
    {
        public string Name { get { return "OMP.WebSocket.RegionModule"; } }
        public Type ReplaceableInterface { get { return null; } }

        private List<Scene> m_Regions = new List<Scene>();
        private IConfig m_Config;
        
        public void Initialise(IConfigSource source)
        {
            m_Config = source.Configs["OMP.WebSocket.RegionModule"];
            
            // Write config to the console
            if (m_Config != null)
            {
                foreach (string key in m_Config.GetKeys())
                    Console.WriteLine("[OMP.WebSocket.RegionModule] {0} = {1}", key, m_Config.Get(key));
            }
        }

        public void Close()
        {
            m_Regions.Clear();
        }

        public void AddRegion(Scene scene)
        {
            m_Regions.Add(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            m_Regions.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
        }
    }
}
