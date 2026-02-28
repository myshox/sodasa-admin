using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>紀錄查詢 Hub — 充值 / 交易 / 金幣 / 郵件 / 活動歷程 / 攤位市場</summary>
    public class RecordsHubForm : Form
    {
        private TabControl _tabs;

        public RecordsHubForm()
        {
            BackColor         = Theme.BgPage;
            ForeColor         = Theme.TextPrimary;
            Font              = Theme.FontBody;
            FormBorderStyle   = FormBorderStyle.None;

            _tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody };
            HubStyle.Apply(_tabs);

            AddTab("💳 充值記錄",     new RechargeHistoryForm());
            AddTab("📊 交易記錄",     new TradeLogForm());
            AddTab("💎 金幣日誌",     new GoldLogForm());
            AddTab("📧 郵件記錄",     new MailHistoryForm());
            AddTab("🔍 活動歷程",     new PlayerHistoryForm());
            AddTab("🏪 攤位 & 市場", new StreetShopForm());

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
