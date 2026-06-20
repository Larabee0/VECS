//using Noesis;
using System;
using System.Numerics;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using VECS.ECS;
using Noesis;
using Vector4 = System.Numerics.Vector4;

namespace VECS.UI
{
    public static class NoesisTypeToField
    {
        public readonly static Type[] BaseFieldTypes = [
            typeof(char),
            typeof(string),

            typeof(sbyte),
            typeof(byte),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),

            typeof(float),
            typeof(double),
            typeof(decimal),

            typeof(bool),
            typeof(Bool3),
            typeof(Bool4),

            typeof(Vector2),
            typeof(Vector2Int),
            typeof(Vector2UInt),

            typeof(Vector3),
            typeof(Vector3Int),
            typeof(Vector3UInt),

            typeof(Vector4),
            typeof(Vector4Int),
            typeof(Vector4UInt),
            typeof(Quaternion),

            typeof(Matrix3x2),
            typeof(Matrix3x3),
            typeof(Matrix4x4),
        ];

        public static readonly Dictionary<Type, Type> TypesToControl = new (){
            
            {typeof(char), typeof(TextBlock)},
            {typeof(string),typeof(TextBlock)},

            {typeof(sbyte), typeof(Vector1Field) },
            {typeof(byte), typeof(Vector1Field) },
            {typeof(int), typeof(Vector1Field) },
            {typeof(uint), typeof(Vector1Field) },
            {typeof(long), typeof(Vector1Field) },
            {typeof(ulong), typeof(Vector1Field) },
            {typeof(short), typeof(Vector1Field) },
            {typeof(ushort), typeof(Vector1Field) },

            {typeof(float), typeof(Vector1Field)},
            {typeof(double), typeof(Vector1Field)},
            {typeof(decimal), typeof(Vector1Field)},

            {typeof(bool), typeof(CheckBox)},
            {typeof(Bool3), typeof(Bool3Field)},
            {typeof(Bool4), typeof(Bool4Field)},

            {typeof(Vector2), typeof(Vector2Field) },
            {typeof(Vector2Int), typeof(Vector2Field) },
            {typeof(Vector2UInt), typeof(Vector2Field) },

            {typeof(Vector3),typeof(Vector3Field) },
            {typeof(Vector3Int),typeof(Vector3Field) },
            {typeof(Vector3UInt),typeof(Vector3Field) },

            {typeof(Vector4), typeof(Vector4Field)},
            {typeof(Vector4Int), typeof(Vector4Field)},
            {typeof(Vector4UInt), typeof(Vector4Field)},
            {typeof(Quaternion), typeof(Vector4Field)},

            {typeof(Matrix3x2), typeof(Matrix3x2Field)},
            {typeof(Matrix3x3), typeof(Matrix3x3Field)},
            {typeof(Matrix4x4), typeof(Matrix4x4Field)}
        };

        public readonly static HashSet<Type> BaseFieldTypesSet = [.. BaseFieldTypes];

        public static FieldHierarhcy GetTypeFields(Type targetType, List<FieldHierarhcy> hierarchy = null)
        {
            FieldHierarhcy main = new()
            {
              Target = targetType  
            };

            hierarchy?.Add(main);

            Console.WriteLine(targetType.Name);
        
            var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for(int i = 0; i < fields.Length; i++)
            {
                Console.WriteLine("{0}.{1}",fields[i].Name,fields[i].FieldType.Name);
                var fieldType = fields[i].FieldType;
                if (!BaseFieldTypesSet.Contains(fieldType))
                {
                    main.Order.Add(new(1, main.Children.Count));
                    GetTypeFields(fieldType, main.Children);
                }
                else
                {
                    main.Order.Add(new(0,main.Types.Count));
                    main.Types.Add(fields[i]);
                }
            }
            Console.WriteLine();
            return main;
        }

        public static TreeViewItem ConstructTree(FieldHierarhcy hierarhcy)
        {
            TreeViewItem treeViewItem = new()
            {
                Header = hierarhcy.Target.Name,
                IsExpanded = true
            };

            for (int i = 0; i < hierarhcy.Order.Count; i++)
            {
                var orderIndices = hierarhcy.Order[i];
                if(orderIndices.X == 0)
                {
                    var typeToBuild = hierarhcy.Types[orderIndices.Y];
                    var instance = (UIElement)Activator.CreateInstance(TypesToControl[typeToBuild.FieldType]);
                    treeViewItem.Items.Add(instance);
                    NameFieldInstance(typeToBuild, instance);
                }
                else
                {
                    treeViewItem.Items.Add(ConstructTree(hierarhcy.Children[orderIndices.Y]));
                }
            }

            return treeViewItem;
        }

        private static void NameFieldInstance(FieldInfo typeToBuild, UIElement instance)
        {
            if (instance is Vector1Field vector1Field)
            {
                vector1Field.Label = typeToBuild.Name;
            }
            else if (instance is CheckBox checkBox)
            {
                checkBox.Content = typeToBuild.Name;
            }
            else if (instance is Bool2Field bool2Field)
            {
                bool2Field.Label = typeToBuild.Name;
            }
            else if (instance is Bool3Field bool3Field)
            {
                bool3Field.Label = typeToBuild.Name;
            }
            else if (instance is Bool4Field bool4Field)
            {
                bool4Field.Label = typeToBuild.Name;
            }
            else if(instance is Vector2Field vector2Field)
            {
                vector2Field.Label = typeToBuild.Name;
            }
            else if(instance is Vector3Field vector3Field)
            {
                vector3Field.Label = typeToBuild.Name;
            }
            else if(instance is Vector4Field vector4Field)
            {
                vector4Field.Label = typeToBuild.Name;
            }
            else if(instance is Matrix3x2Field matrix3X2Field)
            {
                matrix3X2Field.Label = typeToBuild.Name;
            }
            else if(instance is Matrix3x3Field matrix3X3Field)
            {
                matrix3X3Field.Label = typeToBuild.Name;
            }
            else if(instance is Matrix4x4Field matrix4X4Field)
            {
                matrix4X4Field.Label = typeToBuild.Name;
            }
        }

    }

    public class FieldHierarhcy
    {
        public Type Target;
        public List<FieldHierarhcy> Children = [];
        public List<FieldInfo> Types = [];

        public List<Vector2Int> Order = [];
    }
}