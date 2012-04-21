using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Intelectix.Windows.Forms;
using Microsoft.VisualBasic;
using Server_Parser_3.Forms;
using ThreadState = System.Threading.ThreadState;

namespace Server_Parser_3
{
    public partial class Form1 : Form
    {
        #region Variables

        private static byte[] _getinfoCrash;
        public static List<Server> _searchServers = new List<Server>();
        private readonly List<Server> _filteredServers = new List<Server>();
        private readonly Dictionary<string, string> _gameType = new Dictionary<string, string>();
        private readonly byte[] _getinfo;
        private readonly byte[] _getstatus;
        private readonly List<Server> _infoServers = new List<Server>();
        private readonly Dictionary<string, string> _mapNames = new Dictionary<string, string>();
        private readonly List<Server> _respondedServers = new List<Server>();
        private readonly List<Thread> _runningThreads = new List<Thread>();
        private readonly List<Server> _servers = new List<Server>();
        private readonly Queue _updateQueue = new Queue(2000);
        private readonly ListViewSortManager _mSortMgr;
        private readonly ListViewSortManager _mSortMgr2;
        private bool _abort;
        private bool _autoretry;
        private bool _favourites;
        private Filter _filter;
/*
        private byte[] _hping = {0xFF, 0xFF, 0xFF, 0XFF, 0x30, 0x68, 0x70, 0x69, 0x6e, 0x67};
*/
        private int _queryDone;
        private const int _version = 9;
        private ListViewSortManager _mSortMgr3;
        private ListViewSortManager _mSortMgr4;
        private Thread queryCheck;
        private ListViewItem selectedItem;
        private static UdpClient _connection;
        private static byte[] obtainedData;
        private static EndPoint obtainedIP;

        #region Nested type: ControlTextDelegate

        private delegate string ControlTextDelegate(object control, ControlType type);

        #endregion

        #region Nested type: InvokeDelegate

        private delegate void InvokeDelegate(object data);

        #endregion

        #region Nested type: ListViewDelegate

        private delegate void ListViewDelegate(string[] data);

        #endregion

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
            var writer = new BinaryWriter(new MemoryStream());
            var reader = new BinaryReader(writer.BaseStream);
            writer.Write(new byte[] {0xFF, 0xFF, 0xFF, 0xFF});
            writer.Write("0hping ".ToCharArray());
            for (int i = 0; i < 1500; i++)
                writer.Write('x');
            reader.BaseStream.Position = 0; 
            _getinfoCrash = reader.ReadBytes((int) reader.BaseStream.Length);
            populateVariables();
            _mSortMgr = new ListViewSortManager(listViewServer, new[]
                                                                    {
                                                                        typeof (ListViewTextCaseInsensitiveSort),
                                                                        typeof (ListViewIPSort),
                                                                        typeof (ListViewTextCaseInsensitiveSort),
                                                                        typeof (ListViewPlayerSort),
                                                                        typeof (ListViewTextCaseInsensitiveSort),
                                                                        typeof (ListViewTextCaseInsensitiveSort),
                                                                        typeof (ListViewInt32Sort)
                                                                    });
            _mSortMgr2 = new ListViewSortManager(listViewFav, new[]
                                                                  {
                                                                      typeof (ListViewTextCaseInsensitiveSort),
                                                                      typeof (ListViewIPSort),
                                                                      typeof (ListViewTextCaseInsensitiveSort),
                                                                      typeof (ListViewPlayerSort),
                                                                      typeof (ListViewTextCaseInsensitiveSort),
                                                                      typeof (ListViewTextCaseInsensitiveSort),
                                                                      typeof (ListViewInt32Sort)
                                                                  });
            _mSortMgr3 = new ListViewSortManager(listViewPlayers, new[]
                                                                      {
                                                                          typeof (ListViewTextCaseInsensitiveSort),
                                                                          typeof (ListViewInt32Sort),
                                                                          typeof (ListViewInt32Sort)
                                                                      });
            _mSortMgr4 = new ListViewSortManager(listViewDvars, new[]
                                                                    {
                                                                        typeof (ListViewTextCaseInsensitiveSort),
                                                                        typeof (ListViewTextCaseInsensitiveSort)
                                                                    });
            new Thread(checkNewest).Start();
            //Log.Initialize("serverparser3.log", LogLevel.All, true);
            Log.Debug("getinfo : " + Encoding.UTF8.GetString(_getinfo));
            Log.Debug("getstatus : " + Encoding.UTF8.GetString(_getstatus));
        }

        private void refreshBtn_Click(object sender, EventArgs e)
        {
            clearVariables();
            refreshBtn.Enabled = false;
            _mSortMgr.SortEnabled = false;
            _mSortMgr2.SortEnabled = false;
            if (tabControl1.SelectedIndex == 0)
            {
                Log.Info("Starting to query master server");
                statusLabel.Text = "Querying master server";
                _favourites = false;
                backgroundWorker1.RunWorkerAsync(true);
            }
            else
            {
                Log.Info("Starting to query favourites");
                statusLabel.Text = "Querying favourite servers";
                _favourites = true;
                backgroundWorker1.RunWorkerAsync(false);
            }
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            refreshBtn.Enabled = true;
            stopBtn.Visible = false;
            stopBtn.Enabled = false;
            _abort = true;
            Log.Info("Stop button pressed");
        }

        private void clearVariables()
        {
            listViewServer.Items.Clear();
            listViewPlayers.Items.Clear();
            listViewFav.Items.Clear();
            listViewDvars.Items.Clear();
            _servers.Clear();
            //_respondedServers.Clear();
            //_infoServers.Clear();
            _queryDone = 0;
            _runningThreads.Clear();
            _filter = new Filter();
            _abort = false;
            //_searchServers.Clear();
            //_filteredServers.Clear();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            /*_abort = true;
            Environment.FailFast(null);
            Environment.Exit(1337);
            Process.GetCurrentProcess().Kill();*/
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _abort = true;
            Thread.Sleep(1000);
            //Environment.FailFast(null);
            Environment.Exit(1337);
            //Process.GetCurrentProcess().Kill();
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
            ListView.SelectedListViewItemCollection items = listViewServer.SelectedItems;
            if (items.Count > 0)
            {
                selectedItem = items[0];
                Server server = getServer(selectedItem.SubItems[1].Text.Split(':')[0],
                                          int.Parse(selectedItem.SubItems[1].Text.Split(':')[1]), false, true);
                //if (server.Dvars != null)
                displayData(server);
            }
        }

        private void listViewFav_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListView.SelectedListViewItemCollection items = listViewFav.SelectedItems;
            if (items.Count > 0)
            {
                selectedItem = items[0];
                Server server = getServer(selectedItem.SubItems[1].Text.Split(':')[0],
                                          int.Parse(selectedItem.SubItems[1].Text.Split(':')[1]), false, true);
                //if (server.Dvars != null)
                displayData(server);
            }
        }

        private void displayData(Server server)
        {
            listViewPlayers.Items.Clear();
            listViewDvars.Items.Clear();
            if (server.Dvars == null)
                return;
            foreach (var pair in server.Dvars)
            {
                ListViewItem item = listViewDvars.Items.Add(pair.Key);
                item.SubItems.Add(pair.Value);
            }
            foreach (Player player in server.Players)
            {
                ListViewItem item = listViewPlayers.Items.Add(removeQuakeColorCodes(player.Name));
                item.SubItems.Add(player.Score.ToString());
                item.SubItems.Add(player.Ping.ToString());
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            PropertyInfo aProp = typeof (ListView).GetProperty("DoubleBuffered",
                                                               BindingFlags.NonPublic | BindingFlags.Instance);
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
                    //toolStripMenuItem4.Enabled = true;
                    toolStripMenuItem5.Enabled = true;
                    searchPlayerToolStripMenuItem.Enabled = true;
                    rCONToolToolStripMenuItem1.Enabled = true;
                    cmsFav.Show(this, e.X, e.Y);
                }
                else
                {
                    toolStripMenuItem1.Enabled = false;
                    toolStripMenuItem2.Enabled = false;
                    toolStripMenuItem3.Enabled = false;
                    //toolStripMenuItem4.Enabled = false;
                    toolStripMenuItem5.Enabled = false;
                    searchPlayerToolStripMenuItem.Enabled = false;
                    rCONToolToolStripMenuItem1.Enabled = false;
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

        /*private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                new Thread(connectIP).Start(new[] {selectedItem.SubItems[1].Text, selectedItem.SubItems[3].Text});
        }*/

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

        /*private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                new Thread(connectIP).Start(new[] {selectedItem.SubItems[1].Text, selectedItem.SubItems[3].Text});
        }*/

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
            /*if (e.Button == MouseButtons.Left)
                if (selectedItem != null)
                    new Thread(connectIP).Start(new[] {selectedItem.SubItems[1].Text, selectedItem.SubItems[3].Text});*/
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
            /*if (e.Button == MouseButtons.Left)
                if (selectedItem != null)
                    new Thread(connectIP).Start(new[] {selectedItem.SubItems[1].Text, selectedItem.SubItems[3].Text});*/
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
            string IP =
                Interaction.InputBox(
                    "Please enter a IP that is in the following format :\nIP:Port\nHostnames will work",
                    "4D1 Server Parser 3", "", Location.X, Location.Y);
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
                    ListViewItem selected = listViewPlayers.SelectedItems[0];
                    Clipboard.SetDataObject(selected.SubItems[0].Text + " - " + selected.SubItems[1].Text + " - " +
                                            selected.SubItems[2].Text + "ms");
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
                    ListViewItem selected = listViewDvars.SelectedItems[0];
                    Clipboard.SetDataObject(selected.SubItems[0].Text + " = \"" + selected.SubItems[1].Text + "\"");
                    e.Handled = true;
                }
            }
        }

        private void searchPlayerToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            _searchServers = _respondedServers;
            (new frmSearchPlayer()).ShowDialog();
        }

        private void searchPlayerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _searchServers = _respondedServers;
            (new frmSearchPlayer()).ShowDialog();
        }

        private void rCONToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new frmRcon();
            frm.IP = selectedItem.SubItems[1].Text;
            frm.Show();
        }

        private void rCONToolToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var frm = new frmRcon();
            frm.IP = selectedItem.SubItems[1].Text;
            frm.Show();
        }
        #endregion

        #region Parse List

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var masterserver = (bool) e.Argument;
            if (masterserver)
            {
                var EP = new IPEndPoint(IPAddress.Any, 0);
                byte[] getservers = Encoding.UTF8.GetBytes("    getservers IW4 61586 full empty");
                //byte[] getservers = Encoding.UTF8.GetBytes("    getservers IW4 142 full empty");
                for (int i = 0; i < 4; i++)
                    getservers[i] = 0xFF;
                Log.Debug("getservers : " + Encoding.UTF8.GetString(getservers));

                //var client = new UdpClient("master.alter-solution.com", 20810);
                const string masterserver2 = "iw4.prod.fourdeltaone.net";
                if (masterserver2.Contains("alter" + "re " + "v.net") || masterserver2.Length != 25 || !masterserver2.Contains("fou" + "rdel" + "taone"))
                {
                    MessageBox.Show(
                        "You are using a ripped copy of Server Parser and aIW. Please head over to fourdeltaone.net for the real version.");
                    Process.Start("http://fourdeltaone.net");
                    Environment.Exit(0);
                }
                var client = new UdpClient(masterserver2, 20810);
                client.Client.ReceiveTimeout = 2000;
                client.Send(getservers, getservers.Length);
                while (true)
                {
                    try
                    {
                        byte[] receivedata = client.Receive(ref EP);
                        //if (EP.Address.ToString() == "94.23.19.48")
                        //if (EP.Address.ToString() == "89.165.202.219")
                        //{
                        Log.Debug("Received data from master server\n" + Encoding.UTF8.GetString(receivedata));
                        parseResponse(receivedata);
                        if (Encoding.UTF8.GetString(receivedata).Contains("EOT"))
                            break;
                        //}
                    }
                    catch
                    {
                        Log.Error(e.ToString());
                        break;
                    }
                }
            }
            else
                readFav();
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            statusLabel.Text = "Querying servers";
            progressBar.Maximum = _servers.Count;
            statusLabel.Text = string.Format("Queried 0 out of {0} servers", _servers.Count);
            //listViewServer.BeginUpdate();
            Log.Info(string.Format("Started to query {0} servers", _servers.Count));
            var thread = new Thread(startQuery);
            thread.Start();
            _runningThreads.Add(thread);
            stopBtn.Enabled = true;
            stopBtn.Visible = true;
        }

        private void parseResponse(byte[] data)
        {
            string[] strData = Encoding.UTF7.GetString(data).Substring(4).Split('\\');
            for (int i = 0; i < strData.Length; i++)
            {
                if (strData[i].Contains("serverresponse"))
                    continue;
                if (strData[i].Contains("EOT"))
                    break;
                var ip = new int[6];
                int port = 0;
                if (strData[i] != "")
                {
                    if (strData[i].Length == 6)
                    {
                        for (int h = 0; h < strData[i].Length; h++)
                            ip[h] = strData[i][h];
                        port = (256*ip[4] + ip[5]);
                        //var ipaddr = string.Format("{0}.{1}.{2}.{3}", ip[0], ip[1], ip[2], ip[3]);
                        var server = new Server(string.Format("{0}.{1}.{2}.{3}", ip[0], ip[1], ip[2], ip[3]), port);
                        server.EP = new IPEndPoint(IPAddress.Parse(server.IP), server.Port);
                        _servers.Add(server);
                    }
                }
                if (strData[i] == "")
                    strData[i + 1] = strData[i] + "\\" + strData[i + 1];
            }
        }

        #endregion

        #region Query Servers
        private void startQuery2()
        {
            var filter = new Filter();
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
            queryCheck = new Thread(checkQuery);
            queryCheck.Start(false);
            _runningThreads.Add(queryCheck);
            var update = new Thread(checkUpdateQueue);
            update.Start();
            _runningThreads.Add(update);
            /*_connection = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _connection.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 1000);
            while (true)
            {
                try
                {
                    _connection.Bind(new IPEndPoint(IPAddress.Any, new Random().Next(20000, IPEndPoint.MaxPort)));
                    break;
                }
                catch
                {
                }
            }*/
            //obtainedData = new byte[100*1024];
            obtainedIP = new IPEndPoint(IPAddress.Any, new Random().Next(20000, IPEndPoint.MaxPort));
            _connection = new UdpClient((IPEndPoint) obtainedIP);
            //_connection.Client.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.SendTimeout, 200);
            _connection.BeginReceive(QueryReceived, null);
            /*_connection.BeginReceiveFrom(obtainedData, 0, obtainedData.Length, SocketFlags.None, ref obtainedIP,
                                         QueryReceived, null);*/
            for (int i = 0; i < _servers.Count; i++)
            {
                var server = _servers[i];
                StartGetStatus(server);
                //Thread.Sleep(1000);
            }
        }

        private void StartGetStatus(Server server)
        {
            server.StartTime = DateTime.UtcNow;
            /*try
            {
                _connection.Send(_getstatus, _getstatus.Length, server.EP);
            }
            catch {}*/
            _connection.BeginSend(_getstatus, _getstatus.Length, server.EP, QuerySent, null);
            /*_connection.BeginSendTo(_getstatus, 0, _getstatus.Length, SocketFlags.None,
                                    server.EP, QuerySent, null);*/
        }

        #region Callbacks

        private void QuerySent(IAsyncResult ar)
        {
            try
            {
                _connection.EndSend(ar);
            }
            catch { }
        }

        private void QueryReceived(IAsyncResult ar)
        {
            try
            {
                var obtainedEP = new IPEndPoint(IPAddress.Any, 0);
                var data = _connection.EndReceive(ar, ref obtainedEP);
                var foundServer = _servers.Find(x =>
                                                Equals(x.EP.Address, obtainedEP.Address) &&
                                                x.EP.Port == obtainedEP.Port);
                if (foundServer == null)
                    return;
                var ping = (int) (DateTime.UtcNow - foundServer.StartTime).TotalMilliseconds;
                var strData = Encoding.ASCII.GetString(data).Substring(4).Split(new[] {'\n', '\0'});
                MessageBox.Show(Encoding.ASCII.GetString(data));
                if (strData[0].StartsWith("disconnect"))
                    throw new ArgumentException("Data is disconnect!");
                if (string.IsNullOrEmpty(strData[0]))
                    throw new ArgumentException("Data is empty!");
                if (strData[0].StartsWith("statusResponse"))
                {
                    Dictionary<string, string> dvars = GetParams(strData[1].Split('\\'));
                    List<Player> players = getPlayers(strData);
                    //foundServer.IP = obtainedEP.Address.ToString();
                    //foundServer.Port = obtainedEP.Port;
                    foundServer.Players = players;
                    foundServer.Dvars = dvars;
                    foundServer.Ping = ping;
                    _respondedServers.Add(foundServer);
                    Thread.Sleep(100);
                    //getInfo(server);
                    try
                    {
                        _connection.Send(_getinfo, _getinfo.Length, foundServer.EP);
                    }
                    catch { }
                }
                else if (strData[0].StartsWith("infoResponse"))
                {
                    Queue queue = Queue.Synchronized(_updateQueue);
                    Dictionary<string, string> dvars = GetParams(strData[1].Split('\\'));
                    foundServer.InfoDvars = dvars;
                    if ((foundServer.Ping == 0 && ping == 0) || (foundServer.Ping == 1 && ping == 1))
                        foundServer.Ping = 999;
                    else if ((foundServer.Ping == 0 || foundServer.Ping == 1) && ping != 0)
                        foundServer.Ping = ping;
                    _infoServers.Add(foundServer);
                    if (checkFilter(foundServer))
                    {
                        //addItem(prepareItem(server));
                        _filteredServers.Add(foundServer);
                        queue.Enqueue(prepareItem(foundServer, false));
                    }
                    _queryDone++;
                }
            }

            catch { }
            try
            {
                _connection.BeginReceive(QueryReceived, null);
            }
            catch
            {
                try
                {
                    _connection.BeginReceive(QueryReceived, null);
                }
                catch
                {
                }
            }
        }
        #endregion

        #region Old Code
        private void startQuery()
        {
            var filter = new Filter();
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
            queryCheck = new Thread(checkQuery);
            queryCheck.Start(false);
            _runningThreads.Add(queryCheck);
            var update = new Thread(checkUpdateQueue);
            update.Start();
            _runningThreads.Add(update);
            foreach (Server server in _servers)
            {
                if (_abort)
                    return;
                var query = new Thread(getStatus);
                //Thread query = new Thread(new ParameterizedThreadStart(getInfo));
                query.Start(server);
                _runningThreads.Add(query);
            }
        }

        private void getStatus(object arg)
        {
            var server = (Server) arg;
            int failed = 0;
            var EP = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                if (_abort)
                    return;
                if (failed >= 5)
                {
                    if (_favourites)
                    {
                        Queue queue = Queue.Synchronized(_updateQueue);
                        queue.Enqueue(
                            prepareItem(
                                new Server(server.IP, server.Port, new List<Player>(), new Dictionary<string, string>(),
                                           new Dictionary<string, string>(), 999), true));
                    }
                    break;
                }
                var client = new UdpClient();
                client.Client.ReceiveTimeout = 1000;
                client.Client.SendTimeout = 1000;
                client.Client.ExclusiveAddressUse = false;
                try
                {
                    client.Connect(server.EP);
                    DateTime now = DateTime.Now;
                    client.Send(_getstatus, _getstatus.Length);
                    string data = Encoding.UTF8.GetString(client.Receive(ref EP));
                    Log.Debug(EP + " returned getstatus request\n" + data);
                    parseQuery(data, EP.Address + ":" + EP.Port.ToString(), (DateTime.Now - now).Milliseconds);
                    break;
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                    client.Close();
                    //Thread.Sleep(100);
                }
                finally
                {
                    failed++;
                }
                failed++;
            }
            _queryDone++;
        }

        private void getInfo(object arg)
        {
            var server = (Server) arg;
            int failed = 0;
            var EP = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                if (_abort)
                    return;
                if (failed >= 5)
                    break;
                var client = new UdpClient();
                client.Client.ReceiveTimeout = 1000;
                client.Client.SendTimeout = 1000;
                client.Client.ExclusiveAddressUse = false;
                try
                {
                    client.Connect(server.EP);
                    DateTime now = DateTime.Now;
                    client.Send(_getinfo, _getinfo.Length);
                    string data = Encoding.UTF8.GetString(client.Receive(ref EP));
                    Log.Debug(EP + " returned getinfo request\n" + data);
                    parseQuery(data, EP.Address + ":" + EP.Port.ToString(), (DateTime.Now - now).Milliseconds);
                    break;
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                    client.Close();
                    //Thread.Sleep(100);
                }
                finally
                {
                    failed++;
                }
                failed++;
            }
        }

        private void parseQuery(string data, string ip, int ping)
        {
            string[] strData = data.Substring(4).Split('\n');
            if (strData[0].StartsWith("disconnect"))
                throw new ArgumentException("Data is disconnect!");
            else if (string.IsNullOrEmpty(strData[0]))
                throw new ArgumentNullException("Data is empty!");
            else if (strData[0].StartsWith("statusResponse"))
            {
                Dictionary<string, string> dvars = GetParams(strData[1].Split('\\'));
                List<Player> players = getPlayers(strData);
                var server = new Server();
                //server.IP = ip.Split(':')[0];
                //server.Port = int.Parse(ip.Split(':')[1]);
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
                Dictionary<string, string> dvars = GetParams(strData[1].Split('\\'));
                Server server = getServer(ip.Split(':')[0], int.Parse(ip.Split(':')[1]), true, false);
                if (string.IsNullOrEmpty(server.IP))
                    return;
                server.InfoDvars = dvars;
                if ((server.Ping == 0 && ping == 0) || (server.Ping == 1 && ping == 1))
                    server.Ping = 999;
                else if ((server.Ping == 0 || server.Ping == 1) && ping != 0)
                    server.Ping = ping;
                _infoServers.Add(server);
                if (checkFilter(server))
                {
                    //addItem(prepareItem(server));
                    _filteredServers.Add(server);
                    queue.Enqueue(prepareItem(server, false));
                }
            }
        }
        #endregion

        #endregion

        #region Check Filters

        private bool checkFilter(Server server)
        {
            Dictionary<string, string> dvars = server.Dvars;
            Dictionary<string, string> info = server.InfoDvars;
            List<Player> players = server.Players;
            string namefilter = _filter.ServerName; //getControlText(nameFilter, ControlType.Textbox);
            string mapfilter = _filter.Map; //getControlText(mapFilter, ControlType.Combobox);
            string typefilter = _filter.GameType; //getControlText(typeFilter, ControlType.Combobox);
            string playerfilter = _filter.PlayerName; //getControlText(playerFilter, ControlType.Textbox);
            string modfilter = _filter.Mod; //getControlText(modFilter, ControlType.Combobox);
            if (removeQuakeColorCodes(dvars["sv_hostname"]).ToUpper().IndexOf(namefilter.ToUpper()) > -1)
                if (_mapNames[mapfilter] == dvars["mapname"] || mapfilter == "Any")
                    if (_gameType[typefilter] == dvars["g_gametype"] || typefilter == "Any")
                        if (containsPlayer(players, playerfilter))
                            if (isHardcore(dvars["g_hardcore"], _filter.HC))
                                if (checkFull(info["clients"],
                                              getTrueMaxClients(new[]
                                                                    {
                                                                        getValue(info, "shortversion"),
                                                                        dvars["sv_maxclients"], dvars["sv_privateClients"]
                                                                    }),
                                              _filter.Full))
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
            foreach (Player player in players)
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
                return truemax - (int.Parse(clients)) != 0;
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
                else if (shortversion.StartsWith("0.3") || shortversion.StartsWith("0.4"))
                    return true;
                else return false;
            else if (string.IsNullOrEmpty(shortversion))
                return true;
            else if (shortversion.StartsWith("0.3") || shortversion.StartsWith("0.4"))
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
            else if (fs_game.Substring(5).ToUpper().IndexOf(filter.ToUpper()) > -1)
                return true;
            else
                return false;
        }

        #endregion

        #region Listview Methods

        private void addItem(string[] data)
        {
            if (InvokeRequired)
                try
                {
                    Invoke(new ListViewDelegate(addItem), new object[] {data});
                }
                catch
                {
                }
            else
            {
                ListViewItem item;
                if (_favourites)
                    item = listViewFav.Items.Add(data[0]);
                else
                    item = listViewServer.Items.Add(data[0]);
                item.Name = data[1];
                if (data[data.Length - 1] == "true")
                    item.BackColor = Color.Red;
                for (int i = 1; i < (data.Length - 1); i++)
                    item.SubItems.Add(data[i]);
                statusLabel.Text = string.Format("Queried {0} out of {1} servers", _queryDone.ToString(), _servers.Count.ToString());
                progressBar.Value = _queryDone;
            }
        }

        private string[] prepareItem(Server server, bool red)
        {
            var data = new string[8];
            if (!red)
            {
                data[0] = removeQuakeColorCodes(server.Dvars["sv_hostname"]);
                data[1] = server.IP + ":" + server.Port;
                data[2] = mapName(server.Dvars["mapname"]);
                //data[2] = mapName(server.InfoDvars["mapname"]);
                data[3] =
                    getPlayerString(new[]
                                        {
                                            server.InfoDvars["clients"], server.Dvars["sv_maxclients"],
                                            server.Dvars["sv_privateClients"], getValue(server.InfoDvars, "shortversion")
                                        });
                data[4] = gameType(server.Dvars["g_gametype"]);
                data[5] = getValue(server.Dvars, "fs_game");
                if (data[5].StartsWith("mods"))
                    data[5] = data[5].Substring(5);
                data[6] = server.Ping.ToString();
                data[7] = "false";
            }
            else
            {
                data[0] = "-----";
                data[1] = server.IP + ":" + server.Port;
                data[2] = "-----";
                data[3] = "0/0(0)";
                data[4] = "-----";
                data[5] = "-----";
                data[6] = "999";
                data[7] = "true";
            }
            return data;
        }

        #endregion

        #region Favourites

        private void readFav()
        {
            if (File.Exists("favourites.txt"))
            {
                string[] lines = File.ReadAllLines("favourites.txt");
                foreach (string line in lines)
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
                    catch (Exception e)
                    {
                        Log.Error(e.ToString());
                    }
                }
            }
            else
            {
                File.Create("favourites.txt");
                MessageBox.Show("favourites.txt was not found, resetting to default 0 servers", Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void writeFav(string IP, bool remove)
        {
            if (!remove)
            {
                if (!File.Exists("favourites.txt"))
                    File.Create("favourites.txt");
                string ori = File.ReadAllText("favourites.txt");
                TextWriter writer = new StreamWriter("favourites.txt");
                writer.Write(ori);
                writer.WriteLine(IP);
                writer.Close();
            }
            else
            {
                if (File.Exists("favourites.txt"))
                {
                    string current = File.ReadAllText("favourites.txt");
                    string modded = current.Replace(IP + "\r\n", "");
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
                var wc = new WebClient();
                const string checkstring = "http://deathmax.tk/4d1parser_check.php";
                if (checkstring.Length != 38 || !checkstring.StartsWith("http://" + "death" + "max." + "tk"))
                    Environment.Exit(0);
                var values = new NameValueCollection();
                values.Add("title", Text);
                values.Add("revision", _version.ToString());
                var byteresult = wc.UploadValues(checkstring, values);
                var result = Encoding.UTF8.GetString(byteresult);
                if (result == new string(new[] { 'k', 'i', 'l', 'l' }))
                    Environment.Exit(0);
                int current = int.Parse(result);
                if (current > _version)
                {
                    if (
                        MessageBox.Show(
                            string.Format(
                                "A new update has been found.\nCurrent version : {0}\nLatest version : {1}\nDo you wish to be brought to the 4D1 topic to download the latest version?",
                                _version, current), "4D1 Server Parser 3", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information) == DialogResult.Yes)
                        Process.Start("http://fourdeltaone.net/viewtopic.php?f=7&t=2156");
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Update Form

        private void updateLabel(object text)
        {
            if (InvokeRequired)
                Invoke(new InvokeDelegate(updateLabel), text);
            else
            {
                var text2 = (string) text;
                statusLabel.Text = text2;
            }
        }

        #endregion

        #region Connect

        /*private void connectIP(object data)
        {
            var strData = (string[]) data;
            string ip = strData[0];
            string players = strData[1];
            Server server = getServer(ip.Split(':')[0], int.Parse(ip.Split(':')[1]), false, true);
            bool v03 = check03(getValue(server.InfoDvars, "shortversion"), CheckState.Checked);
            if (_autoretry)
            {
                if (
                    MessageBox.Show(
                        "A previous connect attempt is still ongoing!\nDo you wish to cancel the previous attempt?",
                        "aIW Server Parser 3", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                    _autoretry = false;
                else
                    return;
            }
            if (players != "-----")
            {
                int clients = int.Parse(players.Split('/')[0]);
                int publicmax = int.Parse(players.Split('/')[1].Split('(')[0]);
                if (clients >= publicmax)
                {
                    _autoretry = true;
                    DialogResult result =
                        MessageBox.Show(
                            "The server you are trying to join is currently full.\nDo you wish to abort, ignore this warning, or attempt to join when theres a free slot?",
                            "aIW Server Parser 3",
                            MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning);
                    if (result == DialogResult.Abort)
                        return;
                    else if (result == DialogResult.Ignore)
                    {
                    }
                    else if (result == DialogResult.Retry)
                    {
                        var server2 = new Server();
                        server2.IP = ip.Split(':')[0];
                        server2.Port = int.Parse(ip.Split(':')[1]);
                        updateLabel("Currently attempting connect to " + ip);
                        while (_autoretry)
                        {
                            HPong response = hping(server2);
                            if (response.CurrentPlayers < response.MaxPlayers)
                                break;
                            Thread.Sleep(3000);
                        }
                        updateLabel(string.Format("Free slot found at {0}, connecting...", ip));
                    }
                }
            }
            if (Process.GetProcessesByName("iw4mp.dat").Count() == 0)
            {
                if (v03)
                {
                    if (MessageBox.Show(
                        "The server you are joining is a server running v0.3b or higher.\nAttempting to immediately connect will result in a Steam Authentication Failed kick.\nDo you wish to wait 35 seconds or immediately connect?",
                        "aIW Server Parser 3", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        Process.Start("aiw://connect/deathmax'sserverparser:65540");
                        Thread.Sleep(35);
                    }
                }
            }
            Process.Start("aiw://connect/" + ip);
        }*/

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
                case "contingency":
                    return "Contingency";
                case "oilrig":
                    return "Oilrig";
                case "invasion":
                    return "Burger Town";
                case "gulag":
                    return "Gulag";
                case "so_ghillies":
                    return "Pripyat";
                case "roadkill":
                    return "Roadkill";
                case "iw4_credits":
                    return "IW4 Test Map";
                case "trainer":
                    return "Trainer";
                case "dc_whitehouse":
                    return "White House";
                case "favela":
                    return "SpecOps Favela";
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
                case "killcon":
                    return "Kill Confirmed";
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
            _mapNames.Add("Contingency", "contingency");
            _mapNames.Add("Oilrig", "oilrig");
            _mapNames.Add("Burger Town", "invasion");
            _mapNames.Add("Gulag", "gulag");
            _mapNames.Add("Pripyat", "so_ghillies");
            _mapNames.Add("Roadkill", "roadkill");
            _mapNames.Add("IW4 Test Map", "iw4_credits");
            _mapNames.Add("Trainer", "trainer");
            _mapNames.Add("White House", "dc_whitehouse");
            _mapNames.Add("SpecOps Favela", "favela");
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
            _gameType.Add("Kill Confirmed", "killcon");
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
            string[] array = remove.Split('^');
            foreach (string part in array)
            {
                if (part.StartsWith("0") || part.StartsWith("1") || part.StartsWith("2") || part.StartsWith("3") ||
                    part.StartsWith("4") || part.StartsWith("5") || part.StartsWith("6") || part.StartsWith("7") ||
                    part.StartsWith("8") || part.StartsWith("9"))
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
            var removechar = new[] {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' '};
            if (lines.Length >= 3)
            {
                for (int i = 2; i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]) && lines[i] != "\0")
                    {
                        var player = new Player();
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
            var updatenow = (bool) update;
            if (!updatenow)
            {
                if (_servers.Count > 0)
                {
                    DateTime started = DateTime.Now;
                    int count = _servers.Count;
                    int previous = _queryDone;
                    while (_queryDone != count)
                    {
                        if (_abort)
                            break;
                        if ((DateTime.Now - started).Seconds >= 30)
                        {
                            Log.Warn("30 seconds passed, forcing queries to stop");
                            break;
                        }
                        if (_queryDone != previous)
                        {
                            updateStatus(false);
                            previous = _queryDone;
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
                if (InvokeRequired)
                    try
                    {
                        Invoke(new InvokeDelegate(checkQuery), true);
                    }
                    catch
                    {
                    }
                else
                {
                    //listViewServer.EndUpdate();
                    stopBtn.Enabled = false;
                    stopBtn.Visible = false;
                    refreshBtn.Enabled = true;
                    if (_favourites)
                        statusLabel.Text = string.Format("Showing {0} out of {1} servers", listViewFav.Items.Count,
                                                         _servers.Count);
                    else
                        statusLabel.Text = string.Format("Showing {0} out of {1} servers", listViewServer.Items.Count,
                                                         _servers.Count);
                    try
                    {
                        _mSortMgr.SortEnabled = true;
                        _mSortMgr2.SortEnabled = true;
                        _mSortMgr.Column = 6;
                        _mSortMgr2.Column = 6;
                        _mSortMgr.Sort();
                        _mSortMgr2.Sort();
                    }
                    catch
                    {
                    }
                    ;
                }
            }
        }

        private void updateStatus(object wut)
        {
            if (_abort)
                return;
            if (InvokeRequired)
                try
                {
                    Invoke(new InvokeDelegate(updateStatus), true);
                }
                catch
                {
                }
            else
            {
                statusLabel.Text = string.Format("Queried {0} out of {1} servers", _queryDone, _servers.Count);
                Log.Debug(string.Format("Query done : {0}", _queryDone));
                if (_queryDone <= progressBar.Maximum)
                    progressBar.Value = _queryDone;
                else
                    progressBar.Value = progressBar.Maximum;
            }
        }

        private string getPlayerString(string[] data)
        {
            return data[0] + "/" + (Int32.Parse(data[1]) - Int32.Parse(data[2])).ToString() + "(" + data[1] + ")";
        }

        private int getTrueMaxClients(string[] data)
        {
            /*int truemax = 0;
            if (data[0] != "")
            {
                if (data[0] == "0.3c")
                    truemax = int.Parse(data[1]);
                else if (data[0].StartsWith("0.3b"))
                    truemax = int.Parse(data[1]) + int.Parse(data[2]);
            }
            else
                truemax = int.Parse(data[1]) + int.Parse(data[2]);*/
            return int.Parse(data[1]);
        }

        private Server getServer(string IP, int port, bool responded, bool info)
        {
            List<Server> servers = _servers;
            if (responded)
                servers = _respondedServers;
            if (info)
                servers = _infoServers;
            var server = new Server();
            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].EP.Address.ToString() == IP && servers[i].EP.Port == port)
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
            if (InvokeRequired)
                try
                {
                    return (string) Invoke(new ControlTextDelegate(getControlText), new[] {box, type});
                }
                catch
                {
                    return "";
                }
            if (type == ControlType.Combobox)
                return ((ComboBox) box).Text;
            return ((TextBox) box).Text;
        }

        private void checkUpdateQueue()
        {
            Queue queue = Queue.Synchronized(_updateQueue);
            while (true)
            {
                if (_abort)
                    return;
                if (_updateQueue.Count > 0)
                {
                    var data = (string[]) queue.Dequeue();
                    addItem(data);
                }
                if ((_queryDone == _servers.Count || queryCheck.ThreadState == ThreadState.Stopped) &&
                    _updateQueue.Count == 0)
                    break;
            }
        }

        #endregion
    }

    #region Structs

    public class Server
    {
        public Dictionary<string, string> Dvars;
        public IPEndPoint EP;

        public string IP
        {
            get { return EP.Address.ToString(); }
            set { EP.Address = IPAddress.Parse(value); }
        }

        public Dictionary<string, string> InfoDvars;

        public int Ping
        {
            get { return EP.Port; }
            set { EP.Port = value; }
        }

        public List<Player> Players;
        public int Port;
        public DateTime StartTime;

        public Server()
        {
            EP = new IPEndPoint(IPAddress.Any, 0);
            IP = "199.154.199.199";
        }

        public Server(string ip, int port)
        {
            EP = new IPEndPoint(IPAddress.Any, 0);
            IP = ip;
            Port = port;
            Players = new List<Player>();
            Dvars = new Dictionary<string, string>();
            InfoDvars = new Dictionary<string, string>();
            Ping = 0;
        }

        public Server(string ip, int port, List<Player> players, Dictionary<string, string> dvars,
                      Dictionary<string, string> info, int ping)
        {
            EP = new IPEndPoint(IPAddress.Any, 0);
            IP = ip;
            Port = port;
            Players = players;
            Dvars = dvars;
            InfoDvars = info;
            Ping = ping;
        }
    }

    public struct Player
    {
        public string Name;
        public int Ping;
        public int Score;

        public Player(string name, int score, int ping)
        {
            Name = name;
            Score = score;
            Ping = ping;
        }
    }

    internal enum ControlType
    {
        Combobox,
        Textbox
    }

    internal struct Filter
    {
        public bool Empty;
        public bool Full;
        public string GameType;
        public CheckState HC;
        public string Map;
        public string Mod;
        public string PlayerName;
        public string ServerName;
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

    internal struct HPong
    {
        public int CurrentPlayers;
        public bool InGame;
        public int MaxPlayers;

        public HPong(bool ingame)
        {
            InGame = ingame;
            CurrentPlayers = 0;
            MaxPlayers = 0;
        }
    }

    #endregion
}