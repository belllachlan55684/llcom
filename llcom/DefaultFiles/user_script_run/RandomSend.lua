--[[
RandomSend
向指定接口发送随机字符串或随机 hex 数据
]]

-- ========== 配置项（修改此处） ==========
-- 发送目标通道：uart=串口 udp-client=UDP客户端 tcp-client=TCP客户端 tcp-ssl-client=TCP SSL客户端 tcp-server=TCP服务端 mqtt=MQTT netlab=socket公共服务端 winusb=WinUSB
local channel = "uart"

-- 发送模式："string"=随机字符串 "hex"=随机十六进制
local mode = "string"

-- 发送长度范围 [minLen, maxLen]（字节数）
local minLen = 10
local maxLen = 50

-- 循环次数，-1 表示无限循环
local loopCount = 10

-- 发送间隔（毫秒）
local sendInterval = 500

-- ========== 以下无需修改 ==========
if minLen > maxLen then minLen, maxLen = maxLen, minLen end

local function randomString(len)
    local t = {}
    for i = 1, len do
        t[i] = string.char(math.random(32, 126))
    end
    return table.concat(t)
end

local function randomHex(len)
    local hex = "0123456789ABCDEF"
    local t = {}
    for i = 1, len * 2 do
        local idx = math.random(1, 16)
        t[i] = hex:sub(idx, idx)
    end
    return table.concat(t):fromHex()
end

local function sendData(n)
    local len = math.random(minLen, maxLen)
    local data
    if mode == "hex" then
        data = randomHex(len)
    else
        data = randomString(len)
    end
    data = "[" .. n .. "]" .. data
    local ok = apiSend(channel, data)
    return ok
end

sys.taskInit(function()
    local count = 0
    while loopCount < 0 or count < loopCount do
        count = count + 1
        sendData(count)
        if loopCount >= 0 and count >= loopCount then
            break
        end
        sys.wait(sendInterval)
    end
end)
