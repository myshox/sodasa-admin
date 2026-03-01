using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>系統管理 Hub — GM 日誌 / GM 權限 / 備份還原 / 工具帳號</summary>
    public class SystemHubForm : Form
    {
        private TabControl _tabs;

        public SystemHubForm()
        {
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.None;

            _tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody };
            HubStyle.Apply(_tabs);

            AddTab("📋 GM 操作日誌", new GmLogForm());
            AddTab("🛡 GM 權限管理", new GmPermForm());
            AddTab("💾 備份還原",    new BackupForm());
            AddTab("🔑 工具帳號",    new GmAdminForm());
            AddTab("🗑 角色回收桶",  new RecycleBinForm());
            AddTab("💻 SQL 查詢",    new SqlQueryForm());

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
