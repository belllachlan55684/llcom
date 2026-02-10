using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace llcom.Pages
{
    class Convert
    {
    }

    /// <summary>
    /// bool正向设置透明度
    /// </summary>
    [ValueConversion(typeof(bool), typeof(float))]
    public class boolOpacity : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? 1.0 : 0.25;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 根据recvScript切换Opacity
    /// </summary>
    [ValueConversion(typeof(string), typeof(float))]
    public class rsOpacity : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? 0.25 : 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// 根据 ShowTimestamp 控制是否显示时间戳：value[0]=TimeText, value[1]=ShowTimestamp
    /// </summary>
    public class ShowTimestampConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return "";
            var timeText = values[0] as string ?? "";
            var showTimestamp = values[1] is bool b && b;
            return showTimestamp ? timeText : "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// ShowTimestampFormat 转 Visibility：0=Collapsed，1/2=Visible
    /// </summary>
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class ShowTimestampVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int f && f == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// 根据 ShowTimestampFormat 控制时间戳+箭头前缀：value[0]=TimeText, value[1]=ArrowText, value[2]=ShowTimestampFormat, value[3]=TimeTextMs
    /// format: 0=不显示时间戳和箭头，1=日期时间+箭头，2=UTC时间戳+箭头
    /// </summary>
    public class ShowTimestampPrefixConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4) return "";
            var timeText = values[0] as string ?? "";
            var arrowText = values[1] as string ?? "";
            var format = values[2] is int f ? f : 1;
            var timeTextMs = values[3] as string ?? "";
            return format switch
            {
                1 => timeText + arrowText,
                2 => timeTextMs + arrowText,
                _ => "",
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 时间戳格式 int ↔ bool?：0↔false 不显示，1↔true 日期时间，2↔null UTC时间戳
    /// 点击顺序：勾选(日期)→方块(不显示)→不选(毫秒)→勾选
    /// </summary>
    [ValueConversion(typeof(int), typeof(bool?))]
    public class showTimestampFormat : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                1 => true,
                2 => null,
                _ => false,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                true => 1,
                null => 2,
                _ => 0,
            };
        }
    }

    /// <summary>
    /// 根据recvScript切换Tooltip
    /// </summary>
    [ValueConversion(typeof(string[]), typeof(string))]
    public class rsTooltip : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            string rs = value[0] as string;
            string t = value[1] as string;
            ResourceDictionary r = App.Current.Resources;
            if (string.IsNullOrEmpty(rs)) return r["QuickSendRecvScriptNil"];
            else return string.Format(r["QuickSendRecvScriptShow"] as string, rs);
        }

        object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// bool正向显示隐藏
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class boolVisibe : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(bool)value)
                return Visibility.Collapsed;
            else
                return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// bool 取反：用于 IsEnabled，true→false, false→true
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// bool反向显示隐藏
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class boolNotVisibe : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return Visibility.Collapsed;
            else
                return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }


    /// <summary>
    /// value[0]=prefix, value[1]=data；当 data 非空时返回 prefix+data，否则返回空字符串
    /// </summary>
    public class PrefixWhenDataExistsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return "";
            var prefix = values[0] as string ?? "";
            var data = values[1] as string ?? "";
            return !string.IsNullOrEmpty(data) ? prefix + data : "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// 字符串为空时 Collapsed，否则 Visible
    /// </summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// bool为true时显示连接，否则显示断开
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class boolConnected : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Disconnect" : "Connect";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// bool为true时显示连接，否则显示断开
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class boolNot : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }


    /// <summary>
    /// showHexFormat: 1=只显示文本, 2=只显示Hex。bool true=Hex, false=文本。
    /// </summary>
    [ValueConversion(typeof(int), typeof(bool?))]
    public class showHexFormat : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                1 => false,
                2 => true,
                _ => false, // 0 或 other 按文本处理
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                true => 2,
                _ => 1,
            };
        }
    }
}
