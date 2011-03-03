using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;
using Intelectix.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
    
namespace Server_Parser_3
{
    public partial class Form1 : Form
    {
        #region Variables
        private ListViewSortManager m_sortMgr;
        private ListViewSortManager m_sortMgr2;
        private ListViewSortManager m_sortMgr3;
        private ListViewSortManager m_sortMgr4;
        Dictionary<string, string> _mapNames = new Dictionary<string, string>();
        Dictionary<string, string> _gameType = new Dictionary<string, string>();
        List<Server> _servers = new List<Server>();
        List<Server> _respondedServers = new List<Server>();
        List<Server> _infoServers = new List<Server>();
        int _queryDone = 0;
        byte[] _getstatus;
        byte[] _getinfo;
        delegate void InvokeDelegate(object data);
        delegate void ListViewDelegate(string[] data);
        delegate string ControlTextDelegate(object control, ControlType type);
        Thread queryCheck;
        bool _favourites = false;
        ListViewItem selectedItem;
        Queue _updateQueue = new Queue(2000); // Now I'm being lazy like NTAuthority, hardcoding a limit.
        Filter _filter = new Filter();
        int _version = 2;
        #endregion

        #region Form Methods
        public Form1()
        {
            InitializeComponent();
            _getstatus = Encoding.UTF8.GetBytes("    getstatus");
            for (int i = 0; i < 4; i++)
                _getstatus[i] = 0xFF;
            _getinfo = Encoding.UTF8.GetBytes("    getinfo");
            for (int i = 0; i < 4; i++)
                _getinfo[i] = 0xFF;
            populateVariables();
            m_sortMgr = new ListViewSortManager(listViewServer, new Type[] {
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewIPSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewPlayerSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewInt32Sort) });
            m_sortMgr2 = new ListViewSortManager(listViewFav, new Type[] {
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewIPSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewPlayerSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewInt32Sort) });
            m_sortMgr3 = new ListViewSortManager(listViewPlayers, new Type[] {
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewInt32Sort),
                typeof(ListViewInt32Sort) });
            m_sortMgr4 = new ListViewSortManager(listViewDvars, new Type[] {
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewTextCaseInsensitiveSort) });
            new Thread(new ThreadStart(checkNewest)).Start();
        }
        private void refreshBtn_Click(object sender, EventArgs e)
        {
            clearVariables();
            refreshBtn.Enabled = false;
            m_sortMgr.SortEnabled = false;
            m_sortMgr2.SortEnabled = false;
            if (tabControl1.SelectedIndex == 0)
            {
                toolStripStatusLabel1.Text = "Querying master server";
                _favourites = false;
                backgroundWorker1.RunWorkerAsync(true);
            }
            else
            {
                toolStripStatusLabel1.Text = "Querying favourite servers";
                _favourites = true;
                backgroundWorker1.RunWorkerAsync(false);
            }
        }
        private void clearVariables()
        {
            listViewServer.Items.Clear();
            listViewPlayers.Items.Clear();
            listViewFav.Items.Clear();
            listViewDvars.Items.Clear();
            _servers.Clear();
            _respondedServers.Clear();
            _infoServers.Clear();
            _queryDone = 0;
            _filter = new Filter();
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void listViewServer_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ListViewHitTestInfo test = listViewServer.HitTest(e.X, e.Y);
                if (test.Item != null)
                    cmsServers.Show(this, e.X, e.Y);
            }
        }
        private void listViewServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            var items = listViewServer.SelectedItems;
            if (items.Count > 0)
            {
                selectedItem = items[0];
                var server = getServer(selectedItem.SubItems[1].Text.Split(':')[0], int.Parse(selectedItem.SubItems[1].Text.Split(':')[1]), false, true);
                if (server.Dvars != null)
                    displayData(server);
            }
        }
        private void listViewFav_SelectedIndexChanged(object sender, EventArgs e)
        {
            var items = listViewFav.SelectedItems;
            if (items.Count > 0)
            {
                selectedItem = items[0];
                var server = getServer(selectedItem.SubItems[1].Text.Split(':')[0], int.Parse(selectedItem.SubItems[1].Text.Split(':')[1]), false, true);
                if (server.Dvars != null)
                    displayData(server);
            }
        }
        private void displayData(Server server)
        {
            listViewPlayers.Items.Clear();
            listViewDvars.Items.Clear();
            foreach (var pair in server.Dvars)
            {
                var item = listViewDvars.Items.Add(pair.Key);
                item.SubItems.Add(pair.Value);
            }
            foreach (var player in server.Players)
            {
                var item = listViewPlayers.Items.Add(removeQuakeColorCodes(player.Name));
                item.SubItems.Add(player.Score.ToString());
                item.SubItems.Add(player.Ping.ToString());
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            PropertyInfo aProp = typeof(ListView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance);
            aProp.SetValue(listViewDvars, true, null);
            aProp.SetValue(listViewServer, true, null);
            aProp.SetValue(listViewPlayers, true, null);
            aProp.SetValue(listViewFav, true, null);
        }
        private void listViewFav_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ListViewHitTestInfo test = listViewFav.HitTest(e.X, e.Y);
                if (test.Item != null)
                {
                    toolStripMenuItem1.Enabled = true;
                    toolStripMenuItem2.Enabled = true;
                    toolStripMenuItem3.Enabled = true;
                    toolStripMenuItem4.Enabled = true;
                    toolStripMenuItem5.Enabled = true;
                    cmsFav.Show(this, e.X, e.Y);
                }
                else
                {
                    toolStripMenuItem1.Enabled = false;
                    toolStripMenuItem2.Enabled = false;
                    toolStripMenuItem3.Enabled = false;
                    toolStripMenuItem4.Enabled = false;
                    toolStripMenuItem5.Enabled = false;
                    cmsFav.Show(this, e.X, e.Y);
                }
            }
        }
        #endregion

        #region Context Menu Strips/Form Events
        private void addToFavouritesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                writeFav(selectedItem.SubItems[1].Text, false);
        }
        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                new Thread(new ParameterizedThreadStart(connectIP)).Start(selectedItem.SubItems[1].Text);
        }
        private void iPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                Clipboard.SetDataObject(selectedItem.SubItems[1].Text);
        }
        private void hostNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                Clipboard.SetDataObject(selectedItem.SubItems[0].Text);
        }
        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                Clipboard.SetDataObject(selectedItem.SubItems[1].Text);
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                Clipboard.SetDataObject(selectedItem.SubItems[0].Text);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                new Thread(new ParameterizedThreadStart(connectIP)).Start(selectedItem.SubItems[1].Text);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
            {
                writeFav(selectedItem.SubItems[1].Text, true);
                refreshBtn_Click(this, null);
            }
        }
        private void listViewServer_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                if (selectedItem != null)
                    new Thread(new ParameterizedThreadStart(connectIP)).Start(selectedItem.SubItems[1].Text);
        }
        private void listViewServer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                Clipboard.SetDataObject(selectedItem.SubItems[1].Text);
                e.Handled = true;
            }
        }
        private void listViewFav_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                if (selectedItem != null)
                    new Thread(new ParameterizedThreadStart(connectIP)).Start(selectedItem.SubItems[1].Text);
        }
        private void listViewFav_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                Clipboard.SetDataObject(selectedItem.SubItems[1].Text);
                e.Handled = true;
            }
        }
        private void addIPToFavouritesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var IP = Microsoft.VisualBasic.Interaction.InputBox("Please enter a IP that is in the following format :\nIP:Port\nHostnames will work", "aIW Server Parser 3", "", this.Location.X, this.Location.Y);
            if (IP != "" && IP.Split(':').Length == 2)
            {
                writeFav(IP, false);
                refreshBtn_Click(this, null);
            }
        }
        private void listViewPlayers_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                if (listViewPlayers.SelectedItems.Count > 0)
                {
                    var selected = listViewPlayers.SelectedItems[0];
                    Clipboard.SetDataObject(selected.SubItems[0].Text + " - " + selected.SubItems[1].Text + " - " + selected.SubItems[2].Text + "ms");
                    e.Handled = true;
                }
            }
        }
        private void listViewDvars_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                if (listViewDvars.SelectedItems.Count > 0)
                {
                    var selected = listViewDvars.SelectedItems[0];
                    Clipboard.SetDataObject(selected.SubItems[0].Text + " = \"" + selected.SubItems[1].Text + "\"");
                    e.Handled = true;
                }
            }
        }
        #endregion

        #region Parse List
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var masterserver = (bool)e.Argument;
            if (masterserver)
            {
                var EP = new IPEndPoint(IPAddress.Any, 0);
                var getservers = Encoding.UTF8.GetBytes("    getservers IW4 142 full empty");
                for (int i = 0; i < 4; i++)
                    getservers[i] = 0xFF;

                UdpClient client = new UdpClient("server.alteriw.net", 20810);
                client.Client.ReceiveTimeout = 1000;
                client.Send(getservers, getservers.Length);
                while (true)
                {
                    var receivedata = client.Receive(ref EP);
                    if (EP.Address.ToString() == "94.23.19.48")
                    {
                        parseResponse(receivedata);
                        if (Encoding.UTF8.GetString(receivedata).Contains("EOT"))
                            break;
                    }
                }
            }
            else
                readFav();
        }
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            toolStripStatusLabel1.Text = "Querying servers";
            progressBar.Maximum = _servers.Count;
            toolStripStatusLabel1.Text = string.Format("Queried 0 out of {0} servers", _servers.Count);
            //listViewServer.BeginUpdate();
            Thread thread = new Thread(new ThreadStart(startQuery));
            thread.Start();
        }
        private void parseResponse(byte[] data)
        {
            var strData = Encoding.UTF7.GetString(data).Substring(4).Split('\\');
            for (int i = 0; i < strData.Length; i++)
            {
                if (strData[i].Contains("serverresponse"))
                    continue;
                else if (strData[i].Contains("EOT"))
                    break;
                else
                {
                    var ip = new int[6];
                    var port = 0;
                    if (strData[i] != "")
                    {
                        if (strData[i].Length == 6)
                        {
                            for (int h = 0; h < strData[i].Length; h++)
                                ip[h] = (int)strData[i][h];
                            port = (256 * ip[4] + ip[5]);
                            Server server = new Server(string.Format("{0}.{1}.{2}.{3}", ip[0], ip[1], ip[2], ip[3]), port);
                            _servers.Add(server);
                        }
                    }
                    if (strData[i] == "")
                        strData[i + 1] = strData[i] + "\\" + strData[i + 1];
                }
            }
        }
        #endregion

        #region Query Servers
        private void startQuery()
        {
            Filter filter = new Filter();
            filter.ServerName = getControlText(nameFilter, ControlType.Textbox);
            filter.Empty = emptyFilter.Checked;
            filter.Full = fullFilter.Checked;
            filter.GameType = getControlText(typeFilter, ControlType.Combobox);
            filter.HC = hcFilter.CheckState;
            filter.Map = getControlText(mapFilter, ControlType.Combobox);
            filter.Mod = getControlText(modFilter, ControlType.Combobox);
            filter.PlayerName = getControlText(playerFilter, ControlType.Textbox);
            filter.v03 = v03Filter.CheckState;
            _filter = filter;
            queryCheck = new Thread(new ParameterizedThreadStart(checkQuery));
            queryCheck.Start(false);
            var update = new Thread(new ThreadStart(checkUpdateQueue));
            update.Start();
            foreach (var server in _servers)
            {
                Thread query = new Thread(new ParameterizedThreadStart(getStatus));
                query.Start(server);
            }
        }
        private void getStatus(object arg)
        {
            var server = (Server)arg;
            var failed = 0;
            var EP = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                UdpClient client = new UdpClient();
                client.Client.ReceiveTimeout = 1000;
                try
                {
                    client.Connect(server.IP, server.Port);
                    var now = DateTime.Now;
                    if (failed >= 3)
                        break;
                    client.Send(_getstatus, _getstatus.Length);
                    var data = Encoding.UTF8.GetString(client.Receive(ref EP));
                    parseQuery(data, EP.Address.ToString() + ":" + EP.Port.ToString(), (DateTime.Now - now).Milliseconds);
                    break;
                }
                catch
                {
                    client.Close();
                    failed++;
                    //Thread.Sleep(100);
                }
            }
            _queryDone++;
        }
        private void getInfo(object arg)
        {
            var server = (Server)arg;
            var failed = 0;
            var EP = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                UdpClient client = new UdpClient();
                client.Client.ReceiveTimeout = 1000;
                try
                {

                    client.Connect(server.IP, server.Port);
                    var now = DateTime.Now;
                    if (failed >= 3)
                        break;
                    client.Send(_getinfo, _getinfo.Length);
                    var data = Encoding.UTF8.GetString(client.Receive(ref EP));
                    parseQuery(data, EP.Address.ToString() + ":" + EP.Port.ToString(), (DateTime.Now - now).Milliseconds);
                    break;
                }
                catch
                {
                    client.Close();
                    failed++;
                    //Thread.Sleep(100);
                }
            }
        }
        private void parseQuery(string data, string ip, int ping)
        {
            var strData = data.Substring(4).Split('\n');
            if (strData[0].StartsWith("disconnect"))
                throw new System.ArgumentException("Data is disconnect!");
            else if (string.IsNullOrEmpty(strData[0]))
                throw new System.ArgumentNullException("Data is empty!");
            else if (strData[0].StartsWith("statusResponse"))
            {
                var dvars = GetParams(strData[1].Split('\\'));
                var players = getPlayers(strData);
                Server server = new Server();
                server.IP = ip.Split(':')[0];
                server.Port = int.Parse(ip.Split(':')[1]);
                server.Players = players;
                server.Dvars = dvars;
                server.Ping = ping;
                _respondedServers.Add(server);
                //Thread.Sleep(100);
                getInfo(server);
            }
            else if (strData[0].StartsWith("infoResponse"))
            {
                Queue queue = Queue.Synchronized(_updateQueue);
                var dvars = GetParams(strData[1].Split('\\'));
                var server = getServer(ip.Split(':')[0], int.Parse(ip.Split(':')[1]), true, false);
                if (string.IsNullOrEmpty(server.IP))
                    return;
                server.InfoDvars = dvars;
                if (server.Ping == 0 && ping == 0)
                    server.Ping = 999;
                else if (server.Ping == 0 && ping != 0)
                    server.Ping = ping;
                _infoServers.Add(server);
                if (checkFilter(server))
                    //addItem(prepareItem(server));
                    queue.Enqueue(prepareItem(server));
            }
        }
        #endregion

        #region Check Filters
        private bool checkFilter(Server server)
        {
            var dvars = server.Dvars;
            var info = server.InfoDvars;
            var players = server.Players;
            var namefilter = _filter.ServerName;//getControlText(nameFilter, ControlType.Textbox);
            var mapfilter = _filter.Map;//getControlText(mapFilter, ControlType.Combobox);
            var typefilter = _filter.GameType;//getControlText(typeFilter, ControlType.Combobox);
            var playerfilter = _filter.PlayerName;//getControlText(playerFilter, ControlType.Textbox);
            var modfilter = _filter.Mod;//getControlText(modFilter, ControlType.Combobox);
            if (removeQuakeColorCodes(dvars["sv_hostname"]).ToUpper().IndexOf(namefilter.ToUpper()) > -1)
                if (_mapNames[mapfilter] == dvars["mapname"] || mapfilter == "Any")
                    if (_gameType[typefilter] == dvars["g_gametype"] || typefilter == "Any")
                        if (containsPlayer(players, playerfilter))
                            if (isHardcore(dvars["g_hardcore"], _filter.HC))
                                if (checkFull(info["clients"], getTrueMaxClients(new string[] { getValue(info, "shortversion"), dvars["sv_maxclients"], dvars["sv_privateClients"] } ), _filter.Full))
                                    if (checkEmpty(info["clients"], _filter.Empty))
                                        if (check03(getValue(info, "shortversion"), _filter.v03))
                                            if (checkMod(getValue(dvars, "fs_game"), modfilter))
                                                return true;
            return false;
        }
        private bool containsPlayer(List<Player> players, string name)
        {
            if (name == "")
                return true;
            foreach (var player in players)
            {
                if (removeQuakeColorCodes(player.Name).ToUpper().IndexOf(name.ToUpper()) > -1)
                    return true;
            }
            return false;
        }
        private bool isHardcore(string g_hardcore, CheckState state)
        {
            if (state == CheckState.Checked)
                if (g_hardcore == "1")
                    return true;
                else
                    return false;
            else if (state == CheckState.Unchecked)
                if (g_hardcore == "0")
                    return true;
                else
                    return false;
            else
                return true;
        }
        private bool checkFull(string clients, int truemax, bool check)
        {
            if (!check)
                if (truemax - (int.Parse(clients)) == 0)
                    return false;
                else
                    return true;
            else
                return true;
        }
        private bool checkEmpty(string clients, bool check)
        {
            if (!check)
                if (clients == "0")
                    return false;
                else
                    return true;
            else
                return true;
        }
        private bool check03(string shortversion, CheckState state)
        {
            if (state == CheckState.Indeterminate)
                return true;
            else if (state == CheckState.Checked)
                if (string.IsNullOrEmpty(shortversion))
                    return false;
                else if (shortversion.StartsWith("0.3"))
                    return true;
                else return false;
            else
                if (string.IsNullOrEmpty(shortversion))
                    return true;
                else if (shortversion.StartsWith("0.3"))
                    return false;
                else return true;
        }
        private bool checkMod(string fs_game, string filter)
        {
            if (filter == "*")
                return true;
            else if (filter == "None")
                if (fs_game != "")
                    return false;
                else
                    return true;
            else
                if (fs_game.Substring(5).ToUpper().IndexOf(filter.ToUpper()) > -1)
                    return true;
                else
                    return false;
        }
        #endregion

        #region Listview Methods
        private void addItem(string[] data)
        {
            if (this.InvokeRequired)
                this.Invoke(new ListViewDelegate(addItem), new object[] { data });
            else
            {
                ListViewItem item;
                if (_favourites)
                    item = listViewFav.Items.Add(data[0]);
                else
                    item = listViewServer.Items.Add(data[0]);
                item.Name = data[1];
                for (int i = 1; i < data.Length; i++)
                    item.SubItems.Add(data[i]);
                var shit = _queryDone;
                //toolStripStatusLabel1.Text = string.Format("Queried {0} out of {1} servers", _queryDone.ToString(), _servers.Count.ToString());
                //progressBar.Value = _queryDone;
            }
        }
        private string[] prepareItem(Server server)
        {
            var data = new string[7];
            data[0] = removeQuakeColorCodes(server.Dvars["sv_hostname"]);
            data[1] = server.IP + ":" + server.Port.ToString();
            data[2] = mapName(server.Dvars["mapname"]);
            data[3] = getPlayerString(new string[] { server.InfoDvars["clients"], server.Dvars["sv_maxclients"], server.Dvars["sv_privateClients"], getValue(server.InfoDvars, "shortversion") });
            data[4] = gameType(server.Dvars["g_gametype"]);
            data[5] = getValue(server.Dvars, "fs_game");
            if (data[5].StartsWith("mods"))
                data[5] = data[5].Substring(5);
            data[6] = server.Ping.ToString();
            return data;
        }
        #endregion

        #region Favourites
        private void readFav()
        {
            if (File.Exists("favourites.txt"))
            {
                var lines = File.ReadAllLines("favourites.txt");
                foreach (var line in lines)
                {
                    try
                    {
                        if (line.StartsWith("//"))
                            continue;
                        if (string.IsNullOrEmpty(line))
                            continue;
                        if (line.Split(':').Length != 2)
                            continue;
                        var server = new Server();
                        server.IP = line.Split(':')[0];
                        server.Port = int.Parse(line.Split(':')[1]);
                        _servers.Add(server);
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                File.Create("favourites.txt");
                MessageBox.Show("favourites.txt was not found, resetting to default 0 servers", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private void writeFav(string IP, bool remove)
        {
            if (!remove)
            {
                if (!File.Exists("favourites.txt"))
                    File.Create("favourites.txt");
                var ori = File.ReadAllText("favourites.txt");
                TextWriter writer = new StreamWriter("favourites.txt");
                writer.Write(ori);
                writer.WriteLine(IP);
                writer.Close();
            }
            else
            {
                if (File.Exists("favourites.txt"))
                {
                    var current = File.ReadAllText("favourites.txt");
                    var modded = current.Replace(IP + "\r\n", "");
                    File.WriteAllText("favourites.txt", modded);
                }
            }
        }
        #endregion

        #region Check for updates
        private void checkNewest()
        {
            try
            {
                WebClient wc = new WebClient();
                var current = int.Parse(wc.DownloadString("http://deathmax.co.cc/aiwparser3_version.txt"));
                if (current > _version)
                {
                    if (MessageBox.Show(string.Format("A new update has been found.\nCurrent version : {0}\nLatest version : {1}\nDo you wish to be brought to the aIW topic to download the latest version?", _version, current), "aIW Server Parser 3", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        Process.Start("http://alteriw.net/viewtopic.php?f=35&t=55404");
                }
            }
            catch
            {
            }
        }
        #endregion

        #region Misc Methods
        #region Describe Raw Data
        private string mapName(string map)
        {
            switch (map)
            {
                case "mp_afghan":
                    return "Afghan";
                case "mp_complex":
                    return "Bailout";
                case "mp_abandon":
                    return "Carnival";
                case "mp_crash":
                    return "Crash";
                case "mp_derail":
                    return "Derail";
                case "mp_estate":
                    return "Estate";
                case "mp_favela":
                    return "Favela";
                case "mp_fuel2":
                    return "Fuel";
                case "mp_highrise":
                    return "Highrise";
                case "mp_invasion":
                    return "Invasion";
                case "mp_checkpoint":
                    return "Karachi";
                case "mp_overgrown":
                    return "Overgrown";
                case "mp_quarry":
                    return "Quarry";
                case "mp_rundown":
                    return "Rundown";
                case "mp_rust":
                    return "Rust";
                case "mp_compact":
                    return "Salvage";
                case "mp_boneyard":
                    return "Scrapyard";
                case "mp_nightshift":
                    return "Skidrow";
                case "mp_storm":
                    return "Storm";
                case "mp_strike":
                    return "Strike";
                case "mp_subbase":
                    return "Sub Base";
                case "mp_terminal":
                    return "Terminal";
                case "mp_trailerpark":
                    return "Trailer Park";
                case "mp_underpass":
                    return "Underpass";
                case "mp_vacant":
                    return "Vacant";
                case "mp_brecourt":
                    return "Wasteland";
                default:
                    return map;
            }
        }
        private string gameType(string type)
        {
            switch (type)
            {
                case "war":
                    return "Team Deathmatch";
                case "dm":
                    return "Free-for-all";
                case "dom":
                    return "Domination";
                case "sab":
                    return "Sabotage";
                case "sd":
                    return "Search & Destroy";
                case "arena":
                    return "Arena";
                case "dd":
                    return "Demolition";
                case "ctf":
                    return "Capture the Flag";
                case "oneflag":
                    return "One-Flag CTF";
                case "gtnw":
                    return "Global Thermo-Nuclear War";
                case "gg":
                    return "Gun Game";
                case "ss":
                    return "Sharpshooter";
                case "oitc":
                    return "One in the Chamber";
                case "koth":
                    return "Headquarters";
                case "vip":
                    return "VIP";
                default:
                    return type;
            }
        }
        #endregion
        #region Populate Variables
        private void populateVariables()
        {
            _mapNames.Add("Any", "");
            _mapNames.Add("Afghan", "mp_afghan");
            _mapNames.Add("Bailout", "mp_complex");
            _mapNames.Add("Carnival", "mp_abandon");
            _mapNames.Add("Crash", "mp_crash");
            _mapNames.Add("Derail", "mp_derail");
            _mapNames.Add("Estate", "mp_estate");
            _mapNames.Add("Favela", "mp_favela");
            _mapNames.Add("Fuel", "mp_fuel2");
            _mapNames.Add("Highrise", "mp_highrise");
            _mapNames.Add("Invasion", "mp_invasion");
            _mapNames.Add("Karachi", "mp_checkpoint");
            _mapNames.Add("Overgrown", "mp_overgrown");
            _mapNames.Add("Quarry", "mp_quarry");
            _mapNames.Add("Rundown", "mp_rundown");
            _mapNames.Add("Rust", "mp_rust");
            _mapNames.Add("Salvage", "mp_compact");
            _mapNames.Add("Scrapyard", "mp_boneyard");
            _mapNames.Add("Skidrow", "mp_nightshift");
            _mapNames.Add("Storm", "mp_storm");
            _mapNames.Add("Strike", "mp_strike");
            _mapNames.Add("Sub Base", "mp_subbase");
            _mapNames.Add("Terminal", "mp_terminal");
            _mapNames.Add("TrailerPark", "mp_trailerpark");
            _mapNames.Add("Underpass", "mp_underpass");
            _mapNames.Add("Vacant", "mp_vacant");
            _mapNames.Add("Wasteland", "mp_brecourt");
            _gameType.Add("Any", "");
            _gameType.Add("Team Deathmatch", "war");
            _gameType.Add("Free-for-all", "dm");
            _gameType.Add("Domination", "dom");
            _gameType.Add("Sabotage", "sab");
            _gameType.Add("Search & Destroy", "sd");
            _gameType.Add("Arena", "arena");
            _gameType.Add("Demolition", "dd");
            _gameType.Add("Capture the Flag", "ctf");
            _gameType.Add("One-Flag CTF", "oneflag");
            _gameType.Add("Global Thermo-Nuclear War", "gtnw");
            _gameType.Add("Headquarters", "koth");
            _gameType.Add("Gun Game", "gg");
            _gameType.Add("Sharpshooter", "ss");
            _gameType.Add("One in the Chamber", "oitc");
            _gameType.Add("VIP", "vip");
            foreach (var pair in _mapNames)
                mapFilter.Items.Add(pair.Key);
            mapFilter.Text = "Any";
            foreach (var pair in _gameType)
                typeFilter.Items.Add(pair.Key);
            typeFilter.Text = "Any";
            modFilter.Items.Add("*");
            modFilter.Items.Add("None");
            modFilter.Text = "*";
        }
        #endregion
        private string removeQuakeColorCodes(string remove)
        {
            string filteredout = "";
            var array = remove.Split('^');
            foreach (string part in array)
            {
                if (part.StartsWith("0") || part.StartsWith("1") || part.StartsWith("2") || part.StartsWith("3") || part.StartsWith("4") || part.StartsWith("5") || part.StartsWith("6") || part.StartsWith("7") || part.StartsWith("8") || part.StartsWith("9"))
                    filteredout += part.Substring(1);
                else
                    filteredout += "^" + part;
            }
            return filteredout.Substring(1);
        }
        private static Dictionary<string, string> GetParams(string[] parts)
        {
            string key, val;
            var paras = new Dictionary<string, string>();

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;

                key = parts[i++];
                val = parts[i];
                paras[key] = val;
            }

            return paras;
        }
        private List<Player> getPlayers(string[] lines)
        {
            var players = new List<Player>();
            var removechar = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ' };
            if (lines.Length >= 3)
            {
                for (int i = 2; i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]) && lines[i] != "\0")
                    {
                        Player player = new Player();
                        player.Name = lines[i].TrimStart(removechar);
                        player.Name = player.Name.TrimStart('"');
                        player.Name = player.Name.TrimEnd('"');
                        player.Score = int.Parse(lines[i].Split(' ')[0]);
                        player.Ping = int.Parse(lines[i].Split(' ')[1]);
                        players.Add(player);
                    }
                }
            }
            return players;
        }
        private void checkQuery(object update)
        {
            bool updatenow = (bool)update;
            if (!updatenow)
            {
                if (_servers.Count > 0)
                {
                    var started = DateTime.Now;
                    int count = _servers.Count;
                    var previous = _queryDone;
                    while (_queryDone != count)
                    {
                        if ((DateTime.Now - started).Seconds >= 45)
                            break;
                        if (_queryDone != previous)
                        {
                            updateStatus(false); previous = _queryDone;
                        }
                        Thread.Sleep(50);
                    }
                    updateStatus(false);
                    checkQuery(true);
                }
                else
                    checkQuery(true);
            }
            else if (updatenow)
            {
                if (this.InvokeRequired)
                    this.Invoke(new InvokeDelegate(checkQuery), true);
                else
                {
                    //listViewServer.EndUpdate();
                    refreshBtn.Enabled = true;
                    if (_favourites)
                        toolStripStatusLabel1.Text = string.Format("Showing {0} out of {1} servers", listViewFav.Items.Count, _servers.Count);
                    else
                        toolStripStatusLabel1.Text = string.Format("Showing {0} out of {1} servers", listViewServer.Items.Count, _servers.Count);
                    try
                    {
                        m_sortMgr.SortEnabled = true;
                        m_sortMgr2.SortEnabled = true;
                        m_sortMgr.Column = 6;
                        m_sortMgr2.Column = 6;
                        m_sortMgr.Sort();
                        m_sortMgr2.Sort();
                    }
                    catch { };
                }
            }
        }
        private void updateStatus(object wut)
        {
            if (this.InvokeRequired)
                this.Invoke(new InvokeDelegate(updateStatus), true);
            else
            {
                toolStripStatusLabel1.Text = string.Format("Queried {0} out of {1} servers", _queryDone, _servers.Count);
                if (_queryDone <= progressBar.Maximum)
                    progressBar.Value = _queryDone;
                else
                    progressBar.Value = progressBar.Maximum;
            }
        }
        private string getPlayerString(string[] data)
        {
            string complete = "";
            complete += data[0] + "/";
            if (data[3] != "")
            {
                if (data[3] == "0.3c")
                    complete += (Int32.Parse(data[1]) - Int32.Parse(data[2])).ToString() + "(" + data[1] + ")";
                else
                    complete += data[1] + "(" + (Int32.Parse(data[2]) + Int32.Parse(data[1])).ToString() + ")";
            }
            else
                complete += data[1] + "(" + (Int32.Parse(data[2]) + Int32.Parse(data[1])).ToString() + ")";

            return complete;
        }
        private int getTrueMaxClients(string[] data)
        {
            int truemax = 0;
            if (data[0] != "")
            {
                if (data[0] == "0.3c")
                    truemax = int.Parse(data[1]);
                else if (data[0].StartsWith("0.3b"))
                    truemax = int.Parse(data[1]) + int.Parse(data[2]);
            }
            else
                truemax = int.Parse(data[1]) + int.Parse(data[2]);
            return truemax;
        }
        private Server getServer(string IP, int port, bool responded, bool info)
        {
            var servers = _servers;
            if (responded)
                servers = _respondedServers;
            if (info)
                servers = _infoServers;
            var server = new Server();
            for(int i = 0; i < servers.Count; i++)
            {
                if (servers[i].IP == IP && servers[i].Port == port)
                {
                    server = servers[i];
                    break;
                }
            }
            return server;
        }
        private string getValue(Dictionary<string, string> dict, string key)
        {
            try
            {
                return dict[key];
            }
            catch
            {
                return "";
            }
        }
        private string getControlText(object box, ControlType type)
        {
            if (this.InvokeRequired)
                return (string)this.Invoke(new ControlTextDelegate(getControlText), new object[] { box, type });
            else
                if (type == ControlType.Combobox)
                    return ((ComboBox)box).Text;
                else return ((TextBox)box).Text;
        }
        private void checkUpdateQueue()
        {
            Queue queue = Queue.Synchronized(_updateQueue);
            while (true)
            {
                if (_updateQueue.Count > 0)
                {
                    var data = (string[])queue.Dequeue();
                    addItem(data);
                }
                if ((_queryDone == _servers.Count || queryCheck.ThreadState == System.Threading.ThreadState.Stopped) && _updateQueue.Count == 0)
                    break;
            }
        }
        private void connectIP(object IP)
        {
            var ip = (string)IP;
            /*long XUID = 0;
            try
            {
                XUID = (0x0110000100000000 | new XUID().GetSteamID());
            }
            catch
            { }
            WebClient wc = new WebClient();
            var response = wc.DownloadString("http://server.alteriw.net:13000/clean/" + XUID.ToString());
            if (response == "valid")
                Process.Start("aiw://connect/" + ip);
            else
                if (MessageBox.Show("Your XUID(" + XUID.ToString("X15") + ") is unclean.\nDo you still wish to continue?", "aIW Server Parser 3", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                    Process.Start("aiw://connect/" + ip);*/
            if (Process.GetProcessesByName("iw4mp.dat").Count() == 0)
            {
                Process.Start("aiw://connect/deathmax'sserverparser:65540");
                Thread.Sleep(35);
            }
            Process.Start("aiw://connect/" + ip);
        }
        #endregion
    }

    #region Structs
    struct Server
    {
        public string IP;
        public int Port;
        public List<Player> Players;
        public Dictionary<string, string> Dvars;
        public Dictionary<string, string> InfoDvars;
        public int Ping;

        /*public Server()
        {
            IP = "";
            Port = 0;
            Players = new List<Player>();
            Dvars = new Dictionary<string, string>();
        }*/
        public Server(string ip, int port)
        {
            IP = ip;
            Port = port;
            Players = new List<Player>();
            Dvars = new Dictionary<string, string>();
            InfoDvars = new Dictionary<string, string>();
            Ping = 0;
        }
        public Server(string ip, int port, List<Player> players, Dictionary<string, string> dvars, Dictionary<string, string> info, int ping)
        {
            IP = ip;
            Port = port;
            Players = players;
            Dvars = dvars;
            InfoDvars = info;
            Ping = ping;
        }
    }
    struct Player
    {
        public string Name;
        public int Score;
        public int Ping;

        /*public Player()
        {
            Name = "";
            Score = 0;
            Ping = 0;
        }*/
        public Player(string name, int score, int ping)
        {
            Name = name;
            Score = score;
            Ping = ping;
        }
    }
    enum ControlType
    {
        Combobox,
        Textbox
    }
    struct Filter
    {
        public string ServerName;
        public string Map;
        public string GameType;
        public string Mod;
        public string PlayerName;
        public CheckState HC;
        public bool Full;
        public bool Empty;
        public CheckState v03;

        public Filter(string HI)
        {
            ServerName = "";
            Map = "";
            GameType = "";
            Mod = "";
            PlayerName = "";
            HC = CheckState.Indeterminate;
            Full = true;
            Empty = true;
            v03 = CheckState.Indeterminate;
        }
    }
    #endregion

    #region Extra Classes
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class ListViewExtensions
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct LVCOLUMN
        {
            public Int32 mask;
            public Int32 cx;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string pszText;
            public IntPtr hbm;
            public Int32 cchTextMax;
            public Int32 fmt;
            public Int32 iSubItem;
            public Int32 iImage;
            public Int32 iOrder;
        }

        private const Int32 HDI_FORMAT = 0x4;
        private const Int32 HDF_SORTUP = 0x400;
        private const Int32 HDF_SORTDOWN = 0x200;
        private const Int32 LVM_GETHEADER = 0x101f;
        private const Int32 HDM_GETITEM = 0x120b;
        private const Int32 HDM_SETITEM = 0x120c;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessageLVCOLUMN(IntPtr hWnd, Int32 Msg, IntPtr wParam, ref LVCOLUMN lPLVCOLUMN);

        public static void SetSortIcon(this System.Windows.Forms.ListView ListViewControl, int ColumnIndex, System.Windows.Forms.SortOrder Order)
        {
            IntPtr ColumnHeader = SendMessage(ListViewControl.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

            for (int ColumnNumber = 0; ColumnNumber <= ListViewControl.Columns.Count - 1; ColumnNumber++)
            {
                IntPtr ColumnPtr = new IntPtr(ColumnNumber);
                LVCOLUMN lvColumn = new LVCOLUMN();
                lvColumn.mask = HDI_FORMAT;
                SendMessageLVCOLUMN(ColumnHeader, HDM_GETITEM, ColumnPtr, ref lvColumn);

                if (!(Order == System.Windows.Forms.SortOrder.None) && ColumnNumber == ColumnIndex)
                {
                    switch (Order)
                    {
                        case System.Windows.Forms.SortOrder.Ascending:
                            lvColumn.fmt &= ~HDF_SORTDOWN;
                            lvColumn.fmt |= HDF_SORTUP;
                            break;
                        case System.Windows.Forms.SortOrder.Descending:
                            lvColumn.fmt &= ~HDF_SORTUP;
                            lvColumn.fmt |= HDF_SORTDOWN;
                            break;
                    }
                }
                else
                {
                    lvColumn.fmt &= ~HDF_SORTDOWN & ~HDF_SORTUP;
                }

                SendMessageLVCOLUMN(ColumnHeader, HDM_SETITEM, ColumnPtr, ref lvColumn);
            }
        }
    }
    class ListViewNF : System.Windows.Forms.ListView
    {
        public ListViewNF()
        {
            //Activate double buffering
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            //Enable the OnNotifyMessage event so we get a chance to filter out 
            // Windows messages before they get to the form's WndProc
            this.SetStyle(ControlStyles.EnableNotifyMessage, true);
        }

        protected override void OnNotifyMessage(Message m)
        {
            //Filter out the WM_ERASEBKGND message
            if (m.Msg != 0x14)
            {
                base.OnNotifyMessage(m);
            }
        }
    }
    #endregion
}