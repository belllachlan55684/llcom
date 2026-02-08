namespace llcom
{
    /// <summary>
    /// 快捷发送目标接口：提供与数据接口选项卡发送按键相同的发送入口
    /// </summary>
    public interface IQuickSendTarget
    {
        bool PerformSendWithData(string text, bool isHex);
    }
}
