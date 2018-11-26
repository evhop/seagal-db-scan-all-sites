using System;
using System.Collections.Generic;
using System.Text;
using WPDatabaseWork.WPClient.View;

namespace WPDatabaseWork.WPClient.Model
{
    public interface IOptionRepository
    {
        void UpdateOptions(IConnection connection, string optionValue, string option_name);
        Options GetOptionSetting(IConnection connection, string optionName);

    }
}
