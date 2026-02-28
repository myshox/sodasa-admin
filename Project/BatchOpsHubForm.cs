using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>批量操作 Hub — 批量發送 / 道具給予 / 批量金幣</summary>
    public class BatchOpsHubForm : Form
    {
        private TabControl _tabs;

        public BatchOpsHubForm()
        {
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.None;

            _tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody };
            HubStyle.Apply(_tabs);

            AddTab("📢 批量發送", new BatchSendForm());
            AddTab("📬 道具給予", new ItemQueueForm());
            AddTab("💰 批量金幣", new BatchGoldForm());

            Controls.Add(_tabs);
        }

        private void AddTab(string title, Form form)
        {
            var tab = new TabPage(title) { BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary, Padding = Padding.Empty };
            form.TopLevel        = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock            = DockStyle.Fill;
            form.BackColor       = Theme.BgPage;
            tab.Controls.Add(form);
            _tabs.TabPages.Add(tab);
            form.Show();
        }
    }
}
