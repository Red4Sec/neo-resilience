using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string[] Wifs { get; } = new string[]
        {
            "L21gbdyDqZkWj77ZQBPfv8Qh3YYZnRxuVbtfVVZJz6pFAyjDURE7",
            "L2MH55Eg1mAx3mN58MUh7tNomFvWMTZEuvzB15Eug7j1TmETgh5d",
            "L27U9s6vp7eRM4nVSjVdaP1apjLbvdN4kk5gm5tjsdxvEiFjCkb4",
            "KzKh3MLDS3biZY1FNWBEYt5nvBcwGN4z2YV5upxNcNa1DYciXhxg",
            "KynGdnE9YYH9LfoL1vAsebRUxCYs6KeF76gerHPo2ZE1icJpda7B",
            "KzicSa5Vu1EPHjhMT4iw8prSb33K4A1umY56bXTAB2kSJiTbEYZw",
            "KxUr2tg4pSmFzxDttp2YLQLPEmgn8LgmbQfq57eorsfhsAPHrwR5"
        };

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            var wifs = section.GetSection("CN_WIFs").Get<string[]>();
            if (wifs != null)
            {
                Wifs = wifs;
            }
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
