//------------------------------------------------------------
//        File:  IDataSerializable.cs
//       Brief:  IDataSerializable
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-03-24
//============================================================

namespace BTCore.Editor
{
    public interface IDataSerializable<T>
    {
        void ImportData(T data);

        T ExportData();
    }
}