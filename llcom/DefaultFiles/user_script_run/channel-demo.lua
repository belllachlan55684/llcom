--[[
通用消息通道示例代码
该功能拓展了lua脚本的控制范围
可以更加灵活地进行自动化测试
]]

-- uart，对应软件自身的串口功能
apiSetCb("uart",function (data)
    log.info("uart received",data)
end)
local sendResult = apiSend("uart","ok!")

-- mqtt，对应 MQTT 选项卡
apiSetCb("mqtt",function (data)
  log.info(
    "mqtt received",
    data.topic,
    data.payload,
    data.qos)
end)
local sendResult = apiSend("mqtt",nil,
{
  topic   = "test",
  payload = "test",
  qos     = 0
})

-- tcp-server，对应 TCP服务端 选项卡
apiSetCb("tcp-server",function (data)
  log.info(
    "tcp-server received",
    data.from,
    data.data)
-- 特殊功能：当data.from为"connected"或"disconnected"时
-- 表示某客户端连上或断开了
-- 此时data.data用来存储data.from本来要存的内容
end)
local sendResult = apiSend("tcp-server","broadcast message!")

-- udp-client，对应 UDP客户端 选项卡
apiSetCb("udp-client",function (data)
  log.info("udp-client received", data)
end)
local sendResult = apiSend("udp-client","send message by lua!")

-- tcp-client，对应 TCP客户端 选项卡
apiSetCb("tcp-client",function (data)
  log.info("tcp-client received", data)
end)
local sendResult = apiSend("tcp-client","send message by lua!")

-- tcp-ssl-client，对应 TCP SSL客户端 选项卡
apiSetCb("tcp-ssl-client",function (data)
  log.info("tcp-ssl-client received", data)
end)
local sendResult = apiSend("tcp-ssl-client","send message by lua!")

-- netlab，对应 socket公共服务端 选项卡
apiSetCb("netlab",function (data)
  log.info(
    "netlab received",
    data.client,
    data.data)
-- 特殊功能：当data.client为"connected"或"disconnected"时
-- 表示某客户端连上或断开了
-- 此时data.data用来存储data.client本来要存的内容
end)
local sendResult = apiSend("netlab",nil,
{
  client = "aioSession--718957913",
  data   = "test data~"
})

-- winusb，对应 winusb 选项卡
apiSetCb("winusb",function (data)
  log.info("winusb received", data)
end)
local sendResult = apiSend("winusb",string.char(0,4))
