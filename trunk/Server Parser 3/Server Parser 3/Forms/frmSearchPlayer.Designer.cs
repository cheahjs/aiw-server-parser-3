namespace Server_Parser_3
{
    partial class frmSearchPlayer
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.listViewFound = new System.Windows.Forms.ListView();
            this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader2 = new System.Windows.Forms.ColumnHeader();
            this.columnHeader3 = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            // 
            // txtSearch
            // 
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtSearch.Location = new System.Drawing.Point(0, 242);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(284, 20);
            this.txtSearch.TabIndex = 0;
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);
            // 
            // listViewFound
            // 
            this.listViewFound.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3});
            this.listViewFound.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewFound.FullRowSelect = true;
            this.listViewFound.GridLines = true;
            this.listViewFound.Location = new System.Drawing.Point(0, 0);
            this.listViewFound.MultiSelect = false;
            this.listViewFound.Name = "listViewFound";
            this.listViewFound.Size = new System.Drawing.Size(284, 242);
            this.listViewFound.TabIndex = 1;
            this.listViewFound.UseCompatibleStateImageBehavior = false;
            this.listViewFound.View = System.Windows.Forms.View.Details;
            this.listViewFound.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listViewFound_MouseDoubleClick);
            this.listViewFound.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listViewFound_KeyDown);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Name";
            this.columnHeader1.Width = 62;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Server Name";
            this.columnHeader2.Width = 91;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Server IP";
            this.columnHeader3.Width = 103;
            // 
            // frmSearchPlayer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Controls.Add(this.listViewFound);
            this.Controls.Add(this.txtSearch);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "frmSearchPlayer";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Search Players";
            this.Load += new System.EventHandler(this.frmSearchPlayer_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ListView listViewFound;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
    }
}