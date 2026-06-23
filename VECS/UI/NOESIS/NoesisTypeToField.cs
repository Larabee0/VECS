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
            {typeof(Matrix4x4), typeof(Matrix4x4Field)},
            {typeof(Enum),typeof(DropDownField)}
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
                
                if (!BaseFieldTypesSet.Contains(fieldType) && !fieldType.IsEnum && !fieldType.IsArray)
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
                    FieldInfo typeToBuild = hierarhcy.Types[orderIndices.Y];
                    FrameworkElement instance;
                    if (typeToBuild.FieldType.IsEnum)
                    {
                        var dropDown = new DropDownField();
                        var enumComponents = typeToBuild.FieldType.GetEnumNames();

                        for (int j = 0; j < enumComponents.Length; j++)
                        {
                            dropDown.ComboBox.Items.Add(new ComboBoxItem(){Content = enumComponents[j]});
                        }

                        dropDown.ComboBox.SelectedIndex = 0;
                        dropDown.ComboBox.Placeholder = "PlaceHold";
                        instance = dropDown;
                    }
                    else if (typeToBuild.FieldType.IsArray)
                    {
                        var arrayTree = new TreeViewItem(){Header = typeToBuild.Name};
                        Type array = typeToBuild.FieldType;
                        var elementType = array.GetElementType();
                        var arrayTypeFields = GetTypeFields(elementType);
                        
                        var arrayTreeItem = ConstructTree(arrayTypeFields);;
                        arrayTree.Items.Add(arrayTreeItem);
                        instance = arrayTree;
                    }
                    else
                    {
                       
                       instance = (FrameworkElement)Activator.CreateInstance(TypesToControl[typeToBuild.FieldType]);
                    }
                    instance.Tag = string.Format("{0}.{1}",typeToBuild.Name,typeToBuild.FieldType.Name);
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
            if (instance is IEditorField customEditorField)
            {
                customEditorField.Label = typeToBuild.Name;
            }
            else if (instance is CheckBox checkBox)
            {
                checkBox.Content = typeToBuild.Name;
            }
        }


        public static void UpdateValues(EntityManager entityManager, TreeViewItem tree, int componentId, Entity entity)
        {
            IComponent component = entityManager.GetComponent(entity,componentId);

            var type = component.GetType();

            var hierarhcy = GetTypeFields(type);
            for (int i = 0; i < hierarhcy.Order.Count; i++)
            {
                var orderIndices = hierarhcy.Order[i];
                
                if(orderIndices.X == 0)
                {
                    UpdateValues((FrameworkElement)tree.Items[i],hierarhcy.Types[orderIndices.Y].GetValue(component));
                }
                else
                {
                    UpdateValues(hierarhcy.Children[orderIndices.Y],(TreeViewItem)tree.Items[i],component);
                }
            }
        }

        private static void UpdateValues(FieldHierarhcy hierarhcy, TreeViewItem node, object instance)
        {
            
            for (int i = 0; i < hierarhcy.Order.Count; i++)
            {
                var orderIndices = hierarhcy.Order[i];
                
                if(orderIndices.X == 0)
                {
                    UpdateValues((FrameworkElement)node.Items[i],hierarhcy.Types[orderIndices.X].GetValue(instance));
                }
                else
                {
                    UpdateValues(hierarhcy.Children[orderIndices.Y],(TreeViewItem)node.Items[i],instance);
                }
            }
        }

        private static void UpdateValues(FrameworkElement frameworkElement, object v)
        {
            if(!TypesToControl.TryGetValue(v.GetType(), out var controlType) && !v.GetType().IsEnum)
            {
                Console.WriteLine("Invalid Control type {0} for frameworkElement {1}",v.GetType().Name,frameworkElement.GetType().Name);
                return;
            }

            if(frameworkElement.GetType() != controlType && !v.GetType().IsEnum)
            {
                Console.WriteLine("Unexpected framework element type  {0} for control type {1}",frameworkElement.GetType().Name,controlType.Name);
                return;
            }
            if(frameworkElement is IEditorField editorField)
            {
                editorField.SetValue(v);
                //Console.WriteLine("Set {0} to {1}",controlType.Name, v);
            }
            else
            {
                Console.WriteLine("Valid control element type does not implement IEditorField");
            }
            // set value somehow
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