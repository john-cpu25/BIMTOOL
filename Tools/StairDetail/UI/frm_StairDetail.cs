using HuyAddin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using System.Linq;

namespace RincoNhan.Tools.StairDetail;

public class frm_StairDetail : System.Windows.Forms.Form
{
	private IContainer components;

	private Label label1;

	private GroupBox groupBox1;

	public TextBox txtThepPhu_KhoangRai;

	public TextBox txtThepChinh_KhoangRai;

	private Label label4;

	private Label label3;

	public ComboBox cb_ThepPhu;

	public ComboBox cb_ThepChinh;

	private Label label2;

	private Button btOK;

	private Button btCancel;

	public CheckBox chb_RebarLandingTop;

	public CheckBox chb_RebarLandingBot;

	public frm_StairDetail(List<Element> l_rbt)
	{
		InitializeComponent();
		foreach (Element item in l_rbt)
		{
			cb_ThepChinh.Items.Add(item.Name);
			cb_ThepPhu.Items.Add(item.Name);
		}
	}

	private void btOK_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
		double result = 0.0;
		if (double.TryParse(txtThepChinh_KhoangRai.Text, out result))
		{
			Settings.Default.ThepChu_Spacing = txtThepChinh_KhoangRai.Text;
		}
		else
		{
			base.DialogResult = DialogResult.Cancel;
		}
		if (double.TryParse(txtThepPhu_KhoangRai.Text, out result))
		{
			Settings.Default.ThepPhu_Spacing = txtThepPhu_KhoangRai.Text;
		}
		else
		{
			base.DialogResult = DialogResult.Cancel;
		}
		Settings.Default.ThepChu = cb_ThepChinh.Text;
		Settings.Default.ThepPhu = cb_ThepPhu.Text;
		Settings.Default.Save();
		if (base.DialogResult == DialogResult.Cancel)
		{
			MessageBox.Show("Sai Giá Trị Đầu Vào !", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void frm_StairDetail_Load(object sender, EventArgs e)
	{
		txtThepChinh_KhoangRai.Text = Settings.Default.ThepChu_Spacing;
		txtThepPhu_KhoangRai.Text = Settings.Default.ThepPhu_Spacing;
		cb_ThepChinh.Text = Settings.Default.ThepChu;
		cb_ThepPhu.Text = Settings.Default.ThepPhu;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frm_StairDetail));
		this.label1 = new System.Windows.Forms.Label();
		this.groupBox1 = new System.Windows.Forms.GroupBox();
		this.chb_RebarLandingBot = new System.Windows.Forms.CheckBox();
		this.chb_RebarLandingTop = new System.Windows.Forms.CheckBox();
		this.txtThepPhu_KhoangRai = new System.Windows.Forms.TextBox();
		this.txtThepChinh_KhoangRai = new System.Windows.Forms.TextBox();
		this.label4 = new System.Windows.Forms.Label();
		this.label3 = new System.Windows.Forms.Label();
		this.cb_ThepPhu = new System.Windows.Forms.ComboBox();
		this.cb_ThepChinh = new System.Windows.Forms.ComboBox();
		this.label2 = new System.Windows.Forms.Label();
		this.btOK = new System.Windows.Forms.Button();
		this.btCancel = new System.Windows.Forms.Button();
		this.groupBox1.SuspendLayout();
		base.SuspendLayout();
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(6, 16);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(64, 13);
		this.label1.TabIndex = 0;
		this.label1.Text = "Thép Chính";
		this.groupBox1.Controls.Add(this.chb_RebarLandingBot);
		this.groupBox1.Controls.Add(this.chb_RebarLandingTop);
		this.groupBox1.Controls.Add(this.txtThepPhu_KhoangRai);
		this.groupBox1.Controls.Add(this.txtThepChinh_KhoangRai);
		this.groupBox1.Controls.Add(this.label4);
		this.groupBox1.Controls.Add(this.label3);
		this.groupBox1.Controls.Add(this.cb_ThepPhu);
		this.groupBox1.Controls.Add(this.cb_ThepChinh);
		this.groupBox1.Controls.Add(this.label2);
		this.groupBox1.Controls.Add(this.label1);
		this.groupBox1.Location = new System.Drawing.Point(12, 12);
		this.groupBox1.Name = "groupBox1";
		this.groupBox1.Size = new System.Drawing.Size(254, 118);
		this.groupBox1.TabIndex = 1;
		this.groupBox1.TabStop = false;
		this.chb_RebarLandingBot.AutoSize = true;
		this.chb_RebarLandingBot.Location = new System.Drawing.Point(9, 97);
		this.chb_RebarLandingBot.Name = "chb_RebarLandingBot";
		this.chb_RebarLandingBot.Size = new System.Drawing.Size(127, 17);
		this.chb_RebarLandingBot.TabIndex = 9;
		this.chb_RebarLandingBot.Text = "Vẽ Thép Landing Bot";
		this.chb_RebarLandingBot.UseVisualStyleBackColor = true;
		this.chb_RebarLandingTop.AutoSize = true;
		this.chb_RebarLandingTop.Checked = true;
		this.chb_RebarLandingTop.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chb_RebarLandingTop.Location = new System.Drawing.Point(9, 74);
		this.chb_RebarLandingTop.Name = "chb_RebarLandingTop";
		this.chb_RebarLandingTop.Size = new System.Drawing.Size(130, 17);
		this.chb_RebarLandingTop.TabIndex = 8;
		this.chb_RebarLandingTop.Text = "Vẽ Thép Landing Top";
		this.chb_RebarLandingTop.UseVisualStyleBackColor = true;
		this.txtThepPhu_KhoangRai.Location = new System.Drawing.Point(185, 43);
		this.txtThepPhu_KhoangRai.Name = "txtThepPhu_KhoangRai";
		this.txtThepPhu_KhoangRai.Size = new System.Drawing.Size(49, 20);
		this.txtThepPhu_KhoangRai.TabIndex = 7;
		this.txtThepPhu_KhoangRai.Text = "200";
		this.txtThepChinh_KhoangRai.Location = new System.Drawing.Point(185, 13);
		this.txtThepChinh_KhoangRai.Name = "txtThepChinh_KhoangRai";
		this.txtThepChinh_KhoangRai.Size = new System.Drawing.Size(49, 20);
		this.txtThepChinh_KhoangRai.TabIndex = 6;
		this.txtThepChinh_KhoangRai.Text = "150";
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(162, 46);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(18, 13);
		this.label4.TabIndex = 5;
		this.label4.Text = "@";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(162, 16);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(18, 13);
		this.label3.TabIndex = 4;
		this.label3.Text = "@";
		this.cb_ThepPhu.FormattingEnabled = true;
		this.cb_ThepPhu.Location = new System.Drawing.Point(76, 43);
		this.cb_ThepPhu.Name = "cb_ThepPhu";
		this.cb_ThepPhu.Size = new System.Drawing.Size(80, 21);
		this.cb_ThepPhu.TabIndex = 3;
		this.cb_ThepPhu.Text = "D10";
		this.cb_ThepChinh.FormattingEnabled = true;
		this.cb_ThepChinh.Location = new System.Drawing.Point(76, 13);
		this.cb_ThepChinh.Name = "cb_ThepChinh";
		this.cb_ThepChinh.Size = new System.Drawing.Size(80, 21);
		this.cb_ThepChinh.TabIndex = 2;
		this.cb_ThepChinh.Text = "D12";
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(6, 46);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(54, 13);
		this.label2.TabIndex = 1;
		this.label2.Text = "Thép Phụ";
		this.btOK.Location = new System.Drawing.Point(110, 136);
		this.btOK.Name = "btOK";
		this.btOK.Size = new System.Drawing.Size(75, 23);
		this.btOK.TabIndex = 2;
		this.btOK.Text = "OK";
		this.btOK.UseVisualStyleBackColor = true;
		this.btOK.Click += new System.EventHandler(btOK_Click);
		this.btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btCancel.Location = new System.Drawing.Point(191, 136);
		this.btCancel.Name = "btCancel";
		this.btCancel.Size = new System.Drawing.Size(75, 23);
		this.btCancel.TabIndex = 3;
		this.btCancel.Text = "Cancel";
		this.btCancel.UseVisualStyleBackColor = true;
		base.AcceptButton = this.btOK;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.CancelButton = this.btCancel;
		base.ClientSize = new System.Drawing.Size(277, 168);
		base.Controls.Add(this.btCancel);
		base.Controls.Add(this.btOK);
		base.Controls.Add(this.groupBox1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "frm_StairDetail";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Stair Detail";
		base.Load += new System.EventHandler(frm_StairDetail_Load);
		this.groupBox1.ResumeLayout(false);
		this.groupBox1.PerformLayout();
		base.ResumeLayout(false);
	}
}




