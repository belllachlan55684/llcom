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
    /// 根据 ShowTimestamp 控制是否显示时间戳+箭头前缀：value[0]=TimeText, value[1]=ArrowText, value[2]=ShowTimestamp
    /// 合并为单一 Run 避免 XAML 标签间空白被保留导致多余空格
    /// </summary>
    public class ShowTimestampPrefixConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3) return "";
            var timeText = values[0] as string ?? "";
            var arrowText = values[1] as string ?? "";
            var showTimestamp = values[2] is bool b && b;
            return showTimestamp ? (timeText + arrowText) : "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
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
    /// bool为true时显示连接，否则显示断开
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
                _ => null,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                false => 1,
                true => 2,
                _ => 0,
            };
        }
    }
}
