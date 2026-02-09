--[[
绘制正弦曲线脚本
向指定接口按固定间隔发送正弦波采样点，每次发送一个点
可配合接收处理脚本「绘制曲线」使用，在曲线页面查看波形
]]

-- ========== 参数配置（修改此处） ==========
-- 发送接口：uart=串口 udp-client=UDP客户端 tcp-client=TCP客户端 tcp-ssl-client=TCP SSL客户端 tcp-server=TCP服务端
local channel = "uart"

-- 幅值（正弦波振幅）
local amplitude = 100

-- 相位角（弧度，0 表示从 0 开始）
local phaseAngle = 0

-- 周期内采样点数（一个完整正弦波包含的点数）
local pointsPerPeriod = 36

-- 发送间隔（毫秒）
local sendInterval = 50

-- 分隔符（每个点后追加，如 "\r\n" 便于绘制曲线脚本按行解析）
local separator = "\r\n"

-- 循环次数，-1 表示无限循环
local loopCount = -1

-- ========== 以下无需修改 ==========
local function sendPoint(value)
    local data = tostring(value) .. separator
    return apiSend(channel, data)
end

sys.taskInit(function()
    log.info("绘制正弦曲线", "通道=" .. channel, "幅值=" .. amplitude, "相位=" .. phaseAngle,
        "周期点数=" .. pointsPerPeriod, "间隔=" .. sendInterval .. "ms",
        "次数=" .. (loopCount < 0 and "无限" or loopCount))
    local count = 0
    local i = 0
    while loopCount < 0 or count < loopCount do
        local y = amplitude * math.sin(2 * math.pi * i / pointsPerPeriod + phaseAngle)
        local ok = sendPoint(y)
        if ok then
            count = count + 1
        end
        i = i + 1
        if i >= pointsPerPeriod then
            i = 0
        end
        if loopCount >= 0 and count >= loopCount then
            break
        end
        sys.wait(sendInterval)
    end
    log.info("绘制正弦曲线", "结束", "共发送 " .. count .. " 个点")
end)
