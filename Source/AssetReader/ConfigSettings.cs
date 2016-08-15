/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
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

namespace AssetReader
{
    public class ConfigSettings
    {
        private Nini.Config.ConfigCollection _settingsFile;
        public Nini.Config.ConfigCollection SettingsFile
        {
            get { return _settingsFile; }
            set { _settingsFile = value; }
        }

        private string m_storageDll;

        public string StorageDll
        {
            get { return m_storageDll; }
            set { m_storageDll = value; }
        }

        private string m_clientstackDll;

        public string ClientstackDll
        {
            get { return m_clientstackDll; }
            set { m_clientstackDll = value; }
        }

        private string m_assetStorage = "local";

        public string AssetStorage
        {
            get { return m_assetStorage; }
            set { m_assetStorage = value; }
        }

        private string m_assetCache;

        public string AssetCache
        {
            get { return m_assetCache; }
            set { m_assetCache = value; }
        }

        protected string m_storageConnectionString;

        public string StorageConnectionString
        {
            get { return m_storageConnectionString; }
            set { m_storageConnectionString = value; }
        }

        protected string m_librariesXMLFile;
        public string LibrariesXMLFile
        {
            get
            {
                return m_librariesXMLFile;
            }
            set
            {
                m_librariesXMLFile = value;
            }
        }
        protected string m_assetSetsXMLFile;
        public string AssetSetsXMLFile
        {
            get
            {
                return m_assetSetsXMLFile;
            }
            set
            {
                m_assetSetsXMLFile = value;
            }
        }

        public string InventoryPlugin;
        public string InventoryCluster;
        public string LegacyInventorySource;
        public bool InventoryMigrationActive;

        public string CoreConnectionString;

        public const uint DefaultAssetServerHttpPort = 8003;
        public const uint DefaultRegionHttpPort = 9000;
        public const uint DefaultInventoryServerHttpPort = 8004;
    }
}
