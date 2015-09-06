namespace Maze
{
    partial class RogueLike
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RogueLike));
            this.pic = new System.Windows.Forms.PictureBox();
            this.buttonUp = new System.Windows.Forms.Button();
            this.buttonLeft = new System.Windows.Forms.Button();
            this.buttonRight = new System.Windows.Forms.Button();
            this.buttonDown = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.buttonStairDown = new System.Windows.Forms.Button();
            this.textBoxConsole = new System.Windows.Forms.TextBox();
            this.buttonUse = new System.Windows.Forms.Button();
            this.listBoxItemlist = new System.Windows.Forms.ListBox();
            this.buttonInventry = new System.Windows.Forms.Button();
            this.buttonWield = new System.Windows.Forms.Button();
            this.buttonWear = new System.Windows.Forms.Button();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItemSaveLoad = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemHelp = new System.Windows.Forms.ToolStripMenuItem();
            this.buttonTakeOffWeapon = new System.Windows.Forms.Button();
            this.buttonTakeOffArmor = new System.Windows.Forms.Button();
            this.buttonDrop = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pic)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pic
            // 
            this.pic.Location = new System.Drawing.Point(12, 25);
            this.pic.Name = "pic";
            this.pic.Size = new System.Drawing.Size(259, 236);
            this.pic.TabIndex = 0;
            this.pic.TabStop = false;
            this.pic.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pic_MouseDown);
            // 
            // buttonUp
            // 
            this.buttonUp.Location = new System.Drawing.Point(439, 30);
            this.buttonUp.Name = "buttonUp";
            this.buttonUp.Size = new System.Drawing.Size(36, 34);
            this.buttonUp.TabIndex = 4;
            this.buttonUp.Text = "↑";
            this.buttonUp.UseVisualStyleBackColor = true;
            this.buttonUp.Click += new System.EventHandler(this.buttonUp_Click);
            // 
            // buttonLeft
            // 
            this.buttonLeft.Location = new System.Drawing.Point(397, 49);
            this.buttonLeft.Name = "buttonLeft";
            this.buttonLeft.Size = new System.Drawing.Size(36, 34);
            this.buttonLeft.TabIndex = 5;
            this.buttonLeft.Text = "←";
            this.buttonLeft.UseVisualStyleBackColor = true;
            this.buttonLeft.Click += new System.EventHandler(this.buttonLeft_Click);
            // 
            // buttonRight
            // 
            this.buttonRight.Location = new System.Drawing.Point(481, 49);
            this.buttonRight.Name = "buttonRight";
            this.buttonRight.Size = new System.Drawing.Size(36, 34);
            this.buttonRight.TabIndex = 6;
            this.buttonRight.Text = "→";
            this.buttonRight.UseVisualStyleBackColor = true;
            this.buttonRight.Click += new System.EventHandler(this.buttonRight_Click);
            // 
            // buttonDown
            // 
            this.buttonDown.Location = new System.Drawing.Point(439, 70);
            this.buttonDown.Name = "buttonDown";
            this.buttonDown.Size = new System.Drawing.Size(36, 34);
            this.buttonDown.TabIndex = 7;
            this.buttonDown.Text = "↓";
            this.buttonDown.UseVisualStyleBackColor = true;
            this.buttonDown.Click += new System.EventHandler(this.buttonDown_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(296, 37);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(74, 12);
            this.labelStatus.TabIndex = 8;
            this.labelStatus.Text = "ステータス表示";
            // 
            // buttonStairDown
            // 
            this.buttonStairDown.Location = new System.Drawing.Point(481, 95);
            this.buttonStairDown.Name = "buttonStairDown";
            this.buttonStairDown.Size = new System.Drawing.Size(36, 34);
            this.buttonStairDown.TabIndex = 9;
            this.buttonStairDown.Text = "＞";
            this.buttonStairDown.UseVisualStyleBackColor = true;
            this.buttonStairDown.Click += new System.EventHandler(this.buttonStairDown_Click);
            // 
            // textBoxConsole
            // 
            this.textBoxConsole.Location = new System.Drawing.Point(295, 150);
            this.textBoxConsole.Multiline = true;
            this.textBoxConsole.Name = "textBoxConsole";
            this.textBoxConsole.Size = new System.Drawing.Size(292, 98);
            this.textBoxConsole.TabIndex = 10;
            // 
            // buttonUse
            // 
            this.buttonUse.Location = new System.Drawing.Point(527, 30);
            this.buttonUse.Name = "buttonUse";
            this.buttonUse.Size = new System.Drawing.Size(36, 34);
            this.buttonUse.TabIndex = 11;
            this.buttonUse.Text = "u";
            this.buttonUse.UseVisualStyleBackColor = true;
            this.buttonUse.Click += new System.EventHandler(this.buttonUse_Click);
            // 
            // listBoxItemlist
            // 
            this.listBoxItemlist.FormattingEnabled = true;
            this.listBoxItemlist.ItemHeight = 12;
            this.listBoxItemlist.Location = new System.Drawing.Point(8, 27);
            this.listBoxItemlist.Name = "listBoxItemlist";
            this.listBoxItemlist.Size = new System.Drawing.Size(513, 124);
            this.listBoxItemlist.TabIndex = 12;
            this.listBoxItemlist.Visible = false;
            // 
            // buttonInventry
            // 
            this.buttonInventry.Location = new System.Drawing.Point(567, 30);
            this.buttonInventry.Name = "buttonInventry";
            this.buttonInventry.Size = new System.Drawing.Size(36, 34);
            this.buttonInventry.TabIndex = 13;
            this.buttonInventry.Text = "i";
            this.buttonInventry.UseVisualStyleBackColor = true;
            this.buttonInventry.Click += new System.EventHandler(this.buttonInventory_Click);
            // 
            // buttonWield
            // 
            this.buttonWield.Location = new System.Drawing.Point(527, 70);
            this.buttonWield.Name = "buttonWield";
            this.buttonWield.Size = new System.Drawing.Size(36, 34);
            this.buttonWield.TabIndex = 14;
            this.buttonWield.Text = "w";
            this.buttonWield.UseVisualStyleBackColor = true;
            this.buttonWield.Click += new System.EventHandler(this.buttonWield_Click);
            // 
            // buttonWear
            // 
            this.buttonWear.Location = new System.Drawing.Point(567, 70);
            this.buttonWear.Name = "buttonWear";
            this.buttonWear.Size = new System.Drawing.Size(36, 34);
            this.buttonWear.TabIndex = 15;
            this.buttonWear.Text = "W";
            this.buttonWear.UseVisualStyleBackColor = true;
            this.buttonWear.Click += new System.EventHandler(this.buttonWear_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.SystemColors.MenuHighlight;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemSaveLoad,
            this.toolStripMenuItemHelp});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(695, 24);
            this.menuStrip1.TabIndex = 16;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItemSaveLoad
            // 
            this.toolStripMenuItemSaveLoad.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveToolStripMenuItem,
            this.loadToolStripMenuItem});
            this.toolStripMenuItemSaveLoad.Name = "toolStripMenuItemSaveLoad";
            this.toolStripMenuItemSaveLoad.Size = new System.Drawing.Size(82, 20);
            this.toolStripMenuItemSaveLoad.Text = "Save/Load";
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(103, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            this.loadToolStripMenuItem.Size = new System.Drawing.Size(103, 22);
            this.loadToolStripMenuItem.Text = "Load";
            this.loadToolStripMenuItem.Click += new System.EventHandler(this.loadToolStripMenuItem_Click);
            // 
            // toolStripMenuItemHelp
            // 
            this.toolStripMenuItemHelp.Name = "toolStripMenuItemHelp";
            this.toolStripMenuItemHelp.Size = new System.Drawing.Size(45, 20);
            this.toolStripMenuItemHelp.Text = "Help";
            this.toolStripMenuItemHelp.Click += new System.EventHandler(this.toolStripMenuItemHelp_Click);
            // 
            // buttonTakeOffWeapon
            // 
            this.buttonTakeOffWeapon.Location = new System.Drawing.Point(525, 110);
            this.buttonTakeOffWeapon.Name = "buttonTakeOffWeapon";
            this.buttonTakeOffWeapon.Size = new System.Drawing.Size(36, 34);
            this.buttonTakeOffWeapon.TabIndex = 17;
            this.buttonTakeOffWeapon.Text = "t";
            this.buttonTakeOffWeapon.UseVisualStyleBackColor = true;
            this.buttonTakeOffWeapon.Click += new System.EventHandler(this.buttonTakeOffWeapon_Click);
            // 
            // buttonTakeOffArmor
            // 
            this.buttonTakeOffArmor.Location = new System.Drawing.Point(567, 110);
            this.buttonTakeOffArmor.Name = "buttonTakeOffArmor";
            this.buttonTakeOffArmor.Size = new System.Drawing.Size(36, 34);
            this.buttonTakeOffArmor.TabIndex = 18;
            this.buttonTakeOffArmor.Text = "T";
            this.buttonTakeOffArmor.UseVisualStyleBackColor = true;
            this.buttonTakeOffArmor.Click += new System.EventHandler(this.buttonTakeOffArmor_Click);
            // 
            // buttonDrop
            // 
            this.buttonDrop.Location = new System.Drawing.Point(609, 30);
            this.buttonDrop.Name = "buttonDrop";
            this.buttonDrop.Size = new System.Drawing.Size(36, 34);
            this.buttonDrop.TabIndex = 19;
            this.buttonDrop.Text = "d";
            this.buttonDrop.UseVisualStyleBackColor = true;
            this.buttonDrop.Click += new System.EventHandler(this.buttonDrop_Click);
            // 
            // RogueLike
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(695, 261);
            this.Controls.Add(this.buttonDrop);
            this.Controls.Add(this.buttonTakeOffArmor);
            this.Controls.Add(this.buttonTakeOffWeapon);
            this.Controls.Add(this.buttonWear);
            this.Controls.Add(this.buttonWield);
            this.Controls.Add(this.buttonInventry);
            this.Controls.Add(this.listBoxItemlist);
            this.Controls.Add(this.buttonUse);
            this.Controls.Add(this.textBoxConsole);
            this.Controls.Add(this.buttonStairDown);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonDown);
            this.Controls.Add(this.buttonRight);
            this.Controls.Add(this.buttonLeft);
            this.Controls.Add(this.buttonUp);
            this.Controls.Add(this.pic);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "RogueLike";
            this.Text = "Rogue Like";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pic)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pic;
        private System.Windows.Forms.Button buttonUp;
        private System.Windows.Forms.Button buttonLeft;
        private System.Windows.Forms.Button buttonRight;
        private System.Windows.Forms.Button buttonDown;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Button buttonStairDown;
        private System.Windows.Forms.TextBox textBoxConsole;
        private System.Windows.Forms.Button buttonUse;
        private System.Windows.Forms.ListBox listBoxItemlist;
        private System.Windows.Forms.Button buttonInventry;
        private System.Windows.Forms.Button buttonWield;
        private System.Windows.Forms.Button buttonWear;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSaveLoad;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadToolStripMenuItem;
        private System.Windows.Forms.Button buttonTakeOffWeapon;
        private System.Windows.Forms.Button buttonTakeOffArmor;
        private System.Windows.Forms.Button buttonDrop;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemHelp;
    }
}

