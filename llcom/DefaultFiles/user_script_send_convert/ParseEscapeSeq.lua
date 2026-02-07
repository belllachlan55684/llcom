-- parse common escape sequences to control chars
-- supports: \n \r \t \\ \0 \a \b \f \v \xNN
local result = uartData
  :gsub("\\x(%x%x)", function(h) return string.char(tonumber(h, 16)) end)
  :gsub("\\(.)", function(c)
    if c == "n" then return "\n"
    elseif c == "r" then return "\r"
    elseif c == "t" then return "\t"
    elseif c == "0" then return "\0"
    elseif c == "\\" then return "\\"
    elseif c == "a" then return "\a"
    elseif c == "b" then return "\b"
    elseif c == "f" then return "\f"
    elseif c == "v" then return "\v"
    else return "\\" .. c
    end
  end)
return result
