using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO;

public class GenAPI : EditorWindow
{
    // 定义需要导出的命名空间
    public static readonly string[] NameSpaces = new string [] { "Game","GameLogic"};
    public static string GenPath = Application.dataPath + "/../GenAPI/";
    
    /// <summary>
    /// 判断类型是否是委托
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static bool IsDelegate(Type type)
    {
        return typeof (MulticastDelegate).IsAssignableFrom(type.BaseType);
    }
    /// <summary>
    /// 是否是编译器自动生成的类
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    static bool CompilerGen(Type t)
    {
        var attr = Attribute.GetCustomAttribute(t, typeof(CompilerGeneratedAttribute));
        return attr != null;
    }

    static void CreateOutputDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        else
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            FileSystemInfo[] fInfo = dir.GetFileSystemInfos();
            foreach (var f in fInfo)
            {
                if (f is FileInfo)
                {
                    File.Delete(f.FullName);
                }
            }
        }
    }
    
    
    [MenuItem("Tools/GenAPI",false)]
    public static void GenAPIFunc()
    {
        CreateOutputDir(GenPath);

        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(
            ass => ass.GetName().Name == "Assembly-CSharp");
        if (assembly == null)
        {
            Debug.LogError("can not get Assembly-CSharp  ");
            return;
        }
        Debug.Log("AssemeblyName = " + assembly.GetName().Name);
        var types = assembly.GetTypes();
        for (int i = 0; i < types.Length; i++)        
        {
            var t = types[i];
            if(CompilerGen(t))
                continue;
            if (t.IsClass)
            {
                if(IsDelegate(t))
                    continue;;
                foreach (var ns in NameSpaces)
                {
                    if (t.Namespace == ns)
                    {
                        GenClass(t,t.Namespace);
                        break;
                    }
                }
            }
        }
        
        Debug.Log("Gen finished!");
    }

    /// <summary>
    /// 获取类型名称
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            Type[] typeArguments = type.GetGenericArguments();
            string str = type.Name.Substring(0,type.Name.IndexOf('`'));
            str += "<";
            for (int i = 0; i < typeArguments.Length; i++)
            {
                Type t = typeArguments[i];
                if (t.IsGenericType)
                    str += GetTypeName(t);
                else
                {
                    str += t.Name;
                    if (i != typeArguments.Length - 1)
                        str += " , ";
                }
            }
            str += ">";
            return str;
        }
        else
        {
            return type.Name;
        }
    }
    
    private static void GenClass(Type classType,string nameSpace)
    {
        string dir = GenPath + nameSpace;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        
        string fileName = dir+ "/" + classType.Name + ".cs";
        FileStream aFile = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("namespace {0} ", nameSpace);
        sb.Append("{\n\n");
        sb.AppendFormat("public Class {0} ", classType.Name);
        sb.Append("{\n");
        //成员属性
        PropertyInfo[] allProperties =  classType.GetProperties(BindingFlags.Public|BindingFlags.Instance | BindingFlags.Static);
        foreach (var prop in allProperties)
        {
            string isStatic = prop.GetAccessors(true)[0].IsStatic ? "static" : "";
            
            sb.AppendFormat("public {0} {1} {2} \n", isStatic, GetTypeName(prop.PropertyType), prop.Name);
        }
        //成员变量
        FieldInfo[] allFields = classType.GetFields(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance);
        foreach (var field in allFields)
        {
            string isStatic = field.IsStatic ? "static" : "";
            sb.AppendFormat("public {0} {1} {2} \n", isStatic, GetTypeName(field.FieldType), field.Name);
        }
        //方法
        MethodInfo[] methodInfos = classType.GetMethods();
        foreach (var info in methodInfos)
        {
            string isStatic = "";
            string type = "public";
            string abStract = "";
            string isVirtual = "";
            string returnType = "";
            if (info.IsPrivate)
                type = "private";
            else if (info.IsPublic)
                type = "public";
            if (info.IsStatic)
                isStatic = "static";
            if (info.IsAbstract)
                abStract = "asbstract";
            if (info.IsVirtual)
                isVirtual = "virtual";
            if (info.IsConstructor)
                returnType = "";
            else
            {
                returnType = info.ReturnType.ToString();
            }

            ParameterInfo[] paras = info.GetParameters();
            string paramStr = "";
            for (int i = 0; i <paras.Length;++i )
            {
                ParameterInfo p = paras[i];
                string str = GetTypeName(p.ParameterType) + " " + p.Name;
                if (i != paras.Length - 1)
                    str += " , ";
                paramStr += str;
            }
            sb.AppendFormat("{0} {1} {2} {3} {4} {5}({6})\n\n", type,isStatic, isVirtual, abStract,returnType,info.Name,paramStr);
           
        }
        sb.Append("}\n");
        sb.Append("}\n");
        byte[] bytes = System.Text.Encoding.Default.GetBytes(sb.ToString());
        aFile.Write(bytes,0,bytes.Length);
        aFile.Close();
        
        //Debug.Log(sb.ToString());
    }
}
