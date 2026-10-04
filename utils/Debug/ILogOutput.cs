using System;

namespace Keel.Utils.Debug
{
    public interface ILogOutput
    {
        void Info(string msg);
        void Warning(string msg);
        void Error(string msg);
        void Exception(Exception e);
        void Exception(string e);
    }
}