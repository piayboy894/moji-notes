namespace Moji.Core;
public static class Seed
{
    public static Workspace Create(bool demo = false)
    {
        List<Notebook> books = [new() { Name = "日常随记", Mode = NoteMode.Text, Color = "#75876D" }, new() { Name = "灵感提示词", Mode = NoteMode.Prompt, Color = "#B2946E" }, new() { Name = "私人密码库", Mode = NoteMode.Password, Color = "#8393AC" }];
        List<Note> notes = [new() { NotebookId = books[0].Id, Title = "把生活，写成喜欢的样子", Content = "# 把生活，写成喜欢的样子\n\n记录不必宏大。一个想法，一段文字，一次小小的发现，都值得拥有自己的位置。\n\n> 慢慢记录，慢慢成为自己。\n\n## 这个星期的小事\n\n- [x] 整理桌面，让想法自由呼吸\n- [ ] 留一个下午，读完那本书\n\n## 值得收藏的事物\n\n| 片刻 | 想留下的东西 |\n| --- | --- |\n| 清晨 | 第一杯咖啡的香气 |\n| 午后 | 窗边移动的光 |", Tags = ["生活", "灵感"], Favorite = true, Pinned = true }, new() { NotebookId = books[1].Id, Title = "让文字更有温度", Content = "你是一位擅长细腻表达的中文编辑。\n\n请润色以下文字：\n\n{{原始文字}}\n\n要求：\n1. 保留原意，使用自然简洁的中文。\n2. 删除重复和空洞的表达。\n3. 适合发布在 {{发布平台}}。\n\n请直接输出润色后的版本。", Tags = ["写作", "润色"], Favorite = true }];
        if (demo)
        {
            notes.AddRange([new() { NotebookId = books[1].Id, Title = "产品设计 · 从想法到方案", Content = "你是一位产品设计师。请围绕 {{产品想法}}，面向 {{目标用户}} 输出：\n\n## 用户与场景\n\n识别三个核心需求及其场景。\n\n## 设计方向\n\n给出信息架构、核心流程和最小可用版本。", Tags = ["产品", "设计"], UpdatedAt = Clock.Now - 3600000 }, new() { NotebookId = books[0].Id, Title = "周末，留一点空白", Content = "## 周末清单\n\n- [ ] 去常去的那家书店\n- [ ] 带相机沿河走走\n- [x] 把工作消息关掉\n\n给自己一点不被安排的时间。", Tags = ["生活"], UpdatedAt = Clock.Now - 7200000 }, new() { NotebookId = books[2].Id, Title = "示例 · GitHub", Username = "hello@example.com", Password = "Example-Only-2026!", Url = "https://github.com", Tags = ["开发"] }]);
        }
        return new() { Notebooks = books, Notes = notes };
    }
    public static readonly (string Name, NoteMode Mode, string Content)[] Templates = [("空白文本", NoteMode.Text, ""), ("提示词模板", NoteMode.Prompt, "你是一位 {{角色}}。\n\n## 任务\n\n{{任务内容}}\n\n## 要求\n\n面向 {{目标受众}}，请以 {{输出格式}} 输出。"), ("账号密码", NoteMode.Password, ""), ("每周计划", NoteMode.Text, "## 本周重点\n\n- [ ] \n- [ ] \n- [ ] \n\n## 每日安排\n\n| 日期 | 安排 |\n| --- | --- |\n| 周一 | |\n| 周二 | |\n| 周三 | |\n| 周四 | |\n| 周五 | |\n\n## 本周回顾\n\n"), ("会议记录", NoteMode.Text, "## 会议信息\n\n日期：\n参与人：\n\n## 讨论要点\n\n1. \n\n## 行动事项\n\n- [ ] 负责人 · 事项 · 截止日期\n"), ("阅读笔记", NoteMode.Text, "## 书籍信息\n\n书名：\n作者：\n\n## 值得记住的话\n\n> \n\n## 我的想法\n\n\n## 可以付诸行动的事\n\n- [ ] ")];
}
