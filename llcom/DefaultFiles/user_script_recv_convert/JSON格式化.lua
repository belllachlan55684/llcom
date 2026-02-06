-- JSON 格式化接收脚本
-- 将接收到的 JSON 字符串解析后以缩进格式重新输出，便于阅读
-- 若非合法 JSON 则原样返回

local function to_str(data)
    if type(data) == "string" then
        return data
    end
    if type(data) == "table" then
        local t = {}
        for i = 1, #data do
            t[i] = string.char(data[i] or 0)
        end
        return table.concat(t)
    end
    return ""
end

local str = to_str(uartData)
if #str == 0 then
    return uartData
end

-- 去除首尾空白
str = str:match("^%s*(.-)%s*$") or str

local ok, JSON = pcall(require, "JSON")
if not ok or not JSON then
    return uartData
end

local ok2, obj = pcall(JSON.decode, JSON, str)
if not ok2 or not obj then
    return uartData
end

local ok3, pretty = pcall(JSON.encode_pretty, JSON, obj)
if not ok3 or not pretty then
    return uartData
end

return pretty
