using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace Server_Parser_3.Forms
{
    public partial class frmRcon : Form
    {
        #region Variables
        public string IP = "";
        string _password = "";
        string _command = "";
        #endregion

        #region Form Methods
        public frmRcon()
        {
            InitializeComponent();
            txtIP.Text = IP;
        }
        private void frmRcon_Load(object sender, EventArgs e)
        {
            txtIP.Text = IP;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            IP = txtIP.Text;
            _password = txtPass.Text;
            _command = txtCommand.Text;
            if (IP.Split(':').Length == 2)
                if (!string.IsNullOrEmpty(_password) && !string.IsNullOrEmpty(_command))
                {
                    txtOutput.Text += "]" + _command + Environment.NewLine;
                    txtOutput.Text += sendPacket() + Environment.NewLine;
                    txtCommand.Text = "";
                }
                else
                    txtOutput.Text += "Password and/or Command cannot be empty!" + Environment.NewLine;
            else
                txtOutput.Text += "IP must be in the format of IP:Port!" + Environment.NewLine;
        }
        private void txtCommand_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((int)e.KeyChar == (int)Keys.Enter || (int)e.KeyChar == (int)Keys.Return)
            {
                button1_Click(this, null);
                e.Handled = true;
            }
        }
        private void txtOutput_TextChanged(object sender, EventArgs e)
        {
            txtOutput.SelectionStart = txtOutput.Text.Length;
            txtOutput.ScrollToCaret();
        }
        #endregion

        #region Send RCON
        private string sendPacket()
        {
            var returnstring = "";
            try
            {
                var EP = new IPEndPoint(IPAddress.Any, 0);
                UdpClient client = new UdpClient();
                client.Client.ReceiveTimeout = 1000;
                client.Connect(IP.Split(':')[0], int.Parse(IP.Split(':')[1]));
                var send = Encoding.UTF8.GetBytes(string.Format("    rcon \"{0}\" {1}", _password, _command));
                for (int i = 0; i < 4; i++)
                    send[i] = 0xFF;
                client.Send(send, send.Length);
                var data = Encoding.UTF8.GetString(client.Receive(ref EP));
                returnstring = parseRcon(data);
            }
            catch (Exception e)
            {
                returnstring = e.ToString();
            }
            return returnstring;
        }
        private string parseRcon(string data)
        {
            if (data.Substring(4).StartsWith("print"))
                return removeWhiteQuakeColorCodes(data.Substring(4).Substring(6).Replace("\n", Environment.NewLine));
            else
                return data;
        }
        #endregion

        #region Misc Methods
        public string removeWhiteQuakeColorCodes(string remove)
        {
            string filteredout = "";
            var array = remove.Split('^');
            filteredout += array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i].StartsWith("7\""))
                    filteredout += array[i].Substring(1);
                else
                    filteredout += "^" + array[i];
            }
            return filteredout;
        }
        #endregion
    }
}
