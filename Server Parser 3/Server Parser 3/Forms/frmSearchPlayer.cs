using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Intelectix.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace Server_Parser_3
{
    public partial class frmSearchPlayer : Form
    {
        #region Variables
        List<Server> _search = new List<Server>();
        private ListViewSortManager m_sortMgr;
        #endregion

        #region Form Methods
        public frmSearchPlayer()
        {
            InitializeComponent();
            m_sortMgr = new ListViewSortManager(listViewFound, new Type[] {
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewTextCaseInsensitiveSort),
                typeof(ListViewIPSort) });
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            findPlayer(txtSearch.Text);
        }
        private void findPlayer(string name)
        {
            var foundplayers = new Dictionary<string, Server>();
            var servers = _search;
            var randomizer = new Random(Environment.TickCount);
            foreach (var server in servers)
            {
                foreach (var player in server.Players)
                {
                    if (!string.IsNullOrEmpty(player.Name) && player.Name != "\0")
                    {
                        if (player.Name.ToUpper().IndexOf(name.ToUpper()) != -1 || removeQuakeColorCodes(player.Name).ToUpper().IndexOf(name.ToUpper()) != -1)
                        {
                            var playername = player.Name + randomizer.Next(1000000, 9999999);
                            foundplayers.Add(playername, server);
                        }
                    }
                }
            }
            displayPlayers(foundplayers);
        }
        private void displayPlayers(Dictionary<string, Server> players)
        {
            listViewFound.Items.Clear();
            m_sortMgr.SortEnabled = false;
            var length = 0;
            foreach (var item in players)
            {
                try
                {
                    var listitem = listViewFound.Items.Add(removeQuakeColorCodes(item.Key));//(item.Key.Substring(0, item.Key.Length - 7));
                    listitem.SubItems.Add(removeQuakeColorCodes(getValue(item.Value.Dvars, "sv_hostname")));
                    listitem.SubItems.Add(item.Value.IP + ":" + item.Value.Port.ToString());
                    length = (removeQuakeColorCodes(item.Key).Length - 7);
                    listitem.Text = item.Key.Substring(0, length);
                }
                catch { }
            }
            m_sortMgr.SortEnabled = true;
            listViewFound.Sort();
        }
        private void frmSearchPlayer_Load(object sender, EventArgs e)
        {
            _search = Form1._searchServers;
        }
        private void listViewFound_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                var selectedItem = listViewFound.SelectedItems[0];
                if (selectedItem != null)
                {
                    Clipboard.SetDataObject(selectedItem.Text + " - " + selectedItem.SubItems[1].Text + " - " + selectedItem.SubItems[2].Text);
                    e.Handled = true;
                }
            }
        }
        private void listViewFound_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listViewFound.SelectedItems.Count > 0)
            {
                connectIP(listViewFound.SelectedItems[0].SubItems[2].Text);
            }
        }
        #endregion

        #region Misc Methods
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
        private string removeQuakeColorCodes(string remove)
        {
            string filteredout = "";
            var array = remove.Split('^');
            if (array.Length == 1)
                return remove;
            foreach (string part in array)
            {
                if (part.StartsWith("0") || part.StartsWith("1") || part.StartsWith("2") || part.StartsWith("3") || part.StartsWith("4") || part.StartsWith("5") || part.StartsWith("6") || part.StartsWith("7") || part.StartsWith("8") || part.StartsWith("9"))
                    filteredout += part.Substring(1);
                else
                    filteredout += "^" + part;
            }
            return filteredout.Substring(1);
        }
        private void connectIP(object data)
        {
            var strData = (string)data;
            var ip = strData;
            var server = getServer(ip.Split(':')[0], int.Parse(ip.Split(':')[1]));
            var v03 = check03(getValue(server.InfoDvars, "shortversion"), CheckState.Checked);
            if (Process.GetProcessesByName("iw4mp.dat").Count() == 0)
            {
                if (v03)
                {
                    if (MessageBox.Show("The server you are joining is a server running v0.3b or higher.\nAttempting to immediately connect will result in a Steam Authentication Failed kick.\nDo you wish to wait 35 seconds or immediately connect?",
                        "aIW Server Parser 3", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        Process.Start("aiw://connect/deathmax'sserverparser:65540");
                        Thread.Sleep(35);
                    }
                }

            }
            Process.Start("aiw://connect/" + ip);
        }
        private Server getServer(string IP, int port)
        {
            var servers = _search;
            var server = new Server();
            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].IP == IP && servers[i].Port == port)
                {
                    server = servers[i];
                    break;
                }
            }
            return server;
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
            else
                if (string.IsNullOrEmpty(shortversion))
                    return true;
                else if (shortversion.StartsWith("0.3") || shortversion.StartsWith("0.4"))
                    return false;
                else return true;
        }
        #endregion
    }
}
