using System.Net.Http;
namespace Moji.Core;
public static class UserMessages
{
    // Only application-authored validation messages may reach the interface verbatim.
    private static readonly HashSet<string> Known = [
        "GitHub Token 已失效",
        "仓库权限不足或请求受限，请检查 Contents 读写权限",
        "仓库或存档不存在，或当前 Token 无权访问",
        "云端已变化，请刷新列表后重新确认操作",
        "GitHub Token 无效或已过期",
        "GitHub 暂时不可用，请稍后重试",
        "云存档信息损坏",
        "云存档目录损坏或版本不受支持",
        "仓库不存在或 Token 没有读取权限",
        "仓库格式应为 用户名/仓库名",
        "偏好设置无效",
        "加密参数无效",
        "加密文件已损坏",
        "加密文件过大",
        "动态验证码密钥格式不正确",
        "单张图片请控制在 2 MB 以内",
        "变量名称需为 1–40 个字符",
        "变量数据损坏",
        "图片格式不支持",
        "图片过大，请选择 30 MB 以内的图片",
        "无法识别这张图片，请选择 JPG、PNG 或 WebP 图片",
        "图片请控制在 2 MB 以内",
        "存档名称需为 1–60 个字符",
        "存档已达 500 份，请先删除不需要的存档",
        "存档超过容量限制",
        "存档超过容量限制，请先导出较大的图片",
        "密码不正确，或加密文件已损坏",
        "数据 ID 重复",
        "数据为空",
        "数据格式不受支持",
        "数据版本或容量不受支持",
        "无法写入目标文件",
        "无法启动更新程序",
        "无法读取图片",
        "无法读取文件",
        "更新包下载不完整",
        "更新包与当前应用不匹配",
        "更新包大小异常",
        "更新包无法解析",
        "更新包校验失败",
        "更新包校验失败，已取消更新",
        "更新包版本不匹配",
        "更新包签名与当前应用不同，已取消安装",
        "更新包缺少有效的校验值",
        "更新来源不受信任",
        "本地数据过大，请先导出图片",
        "本地文件过大",
        "本机数据尚未打开",
        "空间已变化，请重新读取存档",
        "空间已经锁定",
        "记事本数据损坏",
        "记录数据损坏",
        "设置数据损坏",
        "请先设置 GitHub Token",
        "请选择记事本",
        "请重新选择存档",
        "这不是有效的墨记加密文件",
        "连接已更改，请刷新存档列表",
    ];
    public static string For(Exception error,string fallback="操作未完成，请重试。")
    {
        if(Known.Contains(error.Message))return error.Message;
        return error switch {
            UpdateCheckException => error.Message,
            UnauthorizedAccessException=>"没有写入权限，请换一个保存位置或检查系统权限。",
            HttpRequestException or TaskCanceledException=>"暂时无法连接，请检查网络后重试。",
            System.Text.Json.JsonException or FormatException=>"文件无法读取，请检查文件是否完整。",
            _=>fallback
        };
    }
}
