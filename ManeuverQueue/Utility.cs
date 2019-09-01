﻿using System;
using System.Reflection;
using UnityEngine;

namespace FatHand
{

    public static class Refl
    {
        public static FieldInfo GetField(object obj, int fieldNum)
        {
            int c = 0;
            Debug.Log("GetField 1, fieldNum: " + fieldNum + ", obj.GetType(): " + obj.GetType().ToString());
            Debug.Log("Proceeding");
            foreach (FieldInfo FI in obj.GetType().GetFields(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (c == fieldNum)
                    return FI;
                c++;
            }
            throw new Exception("No such field: " + obj.GetType() + "#" + fieldNum.ToString());
        }
#if false
        public static object GetValue(object obj, int fieldNum)
        {
            return GetField(obj, fieldNum).GetValue(obj);
        }
        public static void SetValue(object obj, int fieldNum, object value)
        {
            GetField(obj, fieldNum).SetValue(obj, value);
        }
#endif

#if true
        public static FieldInfo GetField(object obj, string name)
        {
            Debug.Log("GetField 2, name: " + name);

            var f = obj.GetType().GetField(name, BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) throw new Exception("No such field: " + obj.GetType() + "#" + name);
            return f;
        }
        public static object GetValue(object obj, string name)
        {
            return GetField(obj, name).GetValue(obj);
        }
        public static void SetValue(object obj, string name, object value)
        {
            GetField(obj, name).SetValue(obj, value);
        }
#endif

        public static MethodInfo GetMethod(object obj, int methodnum)
        {

            MethodInfo[] m = obj.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance);
            int c = 0;
            foreach (MethodInfo FI in m)
            {
                if (c == methodnum)
                    return FI;
                c++;
            }

            throw new Exception("No such method: " + obj.GetType() + "#" + methodnum);
        }
        public static object Invoke(object obj, int methodnum, params object[] args)
        {
            return GetMethod(obj, methodnum).Invoke(obj, args);

        }

#if false
		public static MethodInfo GetMethod(object obj, string name) {
			var m = obj.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if(m == null) throw new Exception("No such method: " + obj.GetType() + "#" + name);
			return m;
		}
		public static object Invoke(object obj, string name, params object[] args) {
			return GetMethod(obj, name).Invoke(obj, args);
		}
#endif

    }

}
