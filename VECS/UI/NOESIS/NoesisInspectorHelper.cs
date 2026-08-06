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
    public static class NoesisInspectorHelper
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
            
            {typeof(char), typeof(TextField)},
            {typeof(string),typeof(TextField)},

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

            {typeof(bool), typeof(Bool1Field)},
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

        public static object InspectorTargetObj = null;
        public static Action InspectorTargetObjUpdated;

        public static FieldHierarhcy GetTypeFields(Type targetType)
        {
            var hierarchy = GetTypeFields(targetType, null, null);

            List<List<FieldInfo>> bindingPaths = new(hierarchy.Order.Count);

            ExtractBindingPath(hierarchy, null, ref bindingPaths);
            hierarchy.BindingPaths= bindingPaths;
            return hierarchy;
        }

        private static void ExtractBindingPath(FieldHierarhcy hierarchy, List<FieldInfo> bindingPath, ref List<List<FieldInfo>> bindingPaths)
        {
            for (int i = 0; i < hierarchy.Order.Count; i++)
            {
                List<FieldInfo> binding = bindingPath == null ? [] : [.. bindingPath];
                if (hierarchy.TargetSrc != null)
                {
                    binding.Add(hierarchy.TargetSrc);
                }
                var index = hierarchy.Order[i].Y;
                if (hierarchy.Order[i].X != 0)
                {
                    var child = hierarchy.Children[index];
                    ExtractBindingPath(child, binding,ref bindingPaths);
                }
                else
                {
                    var type = hierarchy.Types[index];
                    binding.Add(type);
                    bindingPaths.Add(binding);
                }
            }
        }

        private static FieldHierarhcy GetTypeFields(Type targetType, FieldInfo targetSrc, List<FieldHierarhcy> hierarchy)
        {
            FieldHierarhcy main = new()
            {
              Target = targetType,
              TargetSrc = targetSrc
            };

            hierarchy?.Add(main);

            var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for(int i = 0; i < fields.Length; i++)
            {
                if(fields[i].GetCustomAttribute(typeof(HideInInspectorAttribute)) != null) continue;
                //Console.WriteLine("{0}.{1}",fields[i].Name,fields[i].FieldType.Name);
                var fieldType = fields[i].FieldType;
                
                if (!BaseFieldTypesSet.Contains(fieldType) && !fieldType.IsEnum && !fieldType.IsArray && fieldType.IsSubclassOf(typeof(Asset)))
                {
                    main.Order.Add(new(1, main.Children.Count));
                    GetTypeFields(fieldType, fields[i],main.Children);
                }
                else
                {
                    main.Order.Add(new(0,main.Types.Count));
                    main.Types.Add(fields[i]);
                }
            }
            //Console.WriteLine();
            return main;
        }
        private static bool _treeConstruction = false;
        public static uint SelectedEntity;
        public static Expander ConstructTree(FieldHierarhcy hierarhcy, List<FieldInfo> bindingPath)
        {
            _treeConstruction = true;
            Expander expander = new()
            {
                Header = hierarhcy.Target.Name,
                IsExpanded = hierarhcy.Order.Count > 0,
                IsEnabled = hierarhcy.Order.Count > 0
            };
            if(hierarhcy.Target.IsAssignableTo(typeof( IComponent)))
            {
                expander.Header = string.Format("{0} : IComponent",expander.Header);
            }

            StackPanel childContainer = new();
            expander.Content = childContainer;

            for (int i = 0; i < hierarhcy.Order.Count; i++)
            {
                var orderIndices = hierarhcy.Order[i];
                bindingPath = hierarhcy.BindingPaths != null ? hierarhcy.BindingPaths[i] : bindingPath;
                if(orderIndices.X == 0)
                {
                    FieldInfo typeToBuild = hierarhcy.Types[orderIndices.Y];
                    bool readonlyAttribute = typeToBuild.GetCustomAttribute(typeof(ReadOnlyInspectorAttribute)) == null;
                    FrameworkElement instance;
                    if (typeToBuild.FieldType.IsEnum)
                    {
                        instance = ConstructEnum(bindingPath, typeToBuild.FieldType);
                    }
                    else if (typeToBuild.FieldType.IsSubclassOf(typeof(Asset)))
                    {
                        instance = ConstructAssetRef(bindingPath, typeToBuild.FieldType);
                    }
                    else if (typeToBuild.FieldType.IsArray)
                    {
                        var arrayExpander = new Expander(){Header = typeToBuild.Name};
                        var arrayStackPanel = new StackPanel();
                        arrayExpander.Content = arrayStackPanel;
                        instance = arrayExpander;
                        List<FieldInfo> localBindingPath = [.. bindingPath];
                        arrayStackPanel.Tag = localBindingPath;
                    }
                    else
                    {
                        instance = ConstructBaseType(bindingPath, typeToBuild.FieldType);
                    }
                    instance.IsEnabled = readonlyAttribute;
                    instance.Tag = string.Format("{0}.{1}",typeToBuild.Name,typeToBuild.FieldType.Name);
                    childContainer.Children.Add(instance);
                    NameFieldInstance(typeToBuild.Name, instance);
                }
                else
                {
                    childContainer.Children.Add(ConstructTree(hierarhcy.Children[orderIndices.Y], bindingPath));
                    _treeConstruction = true;
                }
            }
            _treeConstruction = false;
            return expander;
        }

        private static FrameworkElement ConstructAssetRef(List<FieldInfo> bindingPath, Type typeToBuild)
        {
            FrameworkElement instance;
            var assetRef = new AssetRefField();
            assetRef.SetAssetType(typeToBuild);

            instance = assetRef;
            List<FieldInfo> localBindingPath = [.. bindingPath];
            if (instance is IEditorField editorField)
            {
                editorField.LocalBindingPath = localBindingPath;
                WeakReference weakRef = new(editorField);

                ((IEditorField)weakRef.Target).ValueChanged += BindingFieldValueChanged;
            }
            return instance;
        }

        private static FrameworkElement ConstructEnum(List<FieldInfo> bindingPath, Type typeToBuild)
        {
            FrameworkElement instance;
            var dropDown = new DropDownField();
            var enumComponents = typeToBuild.GetEnumNames();
            FlagsAttribute[] customAttributes = [.. typeToBuild.GetCustomAttributes<FlagsAttribute>()];
            bool flagsEnum = customAttributes != null && customAttributes.Length > 0;
            dropDown.IsFlagsEnum = flagsEnum;
            for (int j = 0; j < enumComponents.Length; j++)
            {
                dropDown.AddRadioButton(enumComponents[j], flagsEnum, enumComponents[j], j == 0);
            }
            instance = dropDown;
            List<FieldInfo> localBindingPath = [.. bindingPath];
            if (instance is IEditorField editorField)
            {
                editorField.LocalBindingPath = localBindingPath;
                WeakReference weakRef = new(editorField);

                ((IEditorField)weakRef.Target).ValueChanged += BindingFieldValueChanged;
            }

            return instance;
        }

        private static FrameworkElement ConstructBaseType(List<FieldInfo> bindingPath, Type typeToBuild)
        {
            FrameworkElement instance = (FrameworkElement)Activator.CreateInstance(TypesToControl[typeToBuild]);
            List<FieldInfo> localBindingPath = [.. bindingPath];
            if (instance is IEditorField editorField)
            {
                editorField.LocalBindingPath = localBindingPath;
                WeakReference weakRef = new(editorField);
                ((IEditorField)weakRef.Target).ValueChanged += BindingFieldValueChanged;
            }

            return instance;
        }

        private static void BindingFieldValueChanged(object s, RoutedEventArgs e)
        {
            if (s == null) return;
            var bindingPath = ((IEditorField)s).LocalBindingPath;
            // the event raised by textbox.textchanged has the new value as accessed directly from textbox.text
            // accessing textbox.text from anywhere also has the new value - its not exclusively the event
            // hover, vector3field._valueX does not update until keyboard focus lost
            if (bindingPath[0].ReflectedType.IsAssignableTo(typeof(IComponent)))
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var targetEntity = entityManager.GetEntityFromId(SelectedEntity);
                if (targetEntity == Entity.Null) return;

                var componentInstance = (IComponent)Activator.CreateInstance(bindingPath[0].ReflectedType);
                componentInstance = entityManager.GetComponent(targetEntity, componentInstance.Id);
                if (componentInstance == null)
                {
                    return;
                }

                if (bindingPath.Any(e => e.FieldType.IsArray))
                {
                    SetValueArray(s, bindingPath, componentInstance);
                }
                else
                {
                    componentInstance = (IComponent)SetValue(s, bindingPath, componentInstance);
                }
                entityManager.SetComponent(targetEntity, componentInstance);
            }
            else if (InspectorTargetObj != null)
            {
                if (bindingPath.Any(e => e.FieldType.IsArray))
                {
                    SetValueArray(s, bindingPath, InspectorTargetObj);
                }
                else
                {
                    SetValue(s,bindingPath, InspectorTargetObj);
                }
                InspectorTargetObjUpdated?.Invoke();
            }
        }

        private static object SetValue(object s, List<FieldInfo> bindingPath, object instance)
        {
            object parent = instance;
            for (int i = 0; i < bindingPath.Count - 1; i++)
            {
                parent = bindingPath[i].GetValue(parent);
            }

            object current = bindingPath[^1].GetValue(parent);
            current = ((VECSEditorControl)s).TryParse(current);
            bindingPath[^1].SetValue(parent, current);

            for (int i = bindingPath.Count - 1; i >= 0; i--)
            {
                object setter = instance;
                for (int j = 0; j < i; j++)
                {
                    setter = bindingPath[j].GetValue(setter);
                }
                bindingPath[i].SetValue(setter, current);
                current = setter;
            }
            return current;
        }

        private static object SetValueArray(object s, List<FieldInfo> bindingPath, object instance)
        {
            FieldHierarhcy elementHierarchy = null;
            var elementType = bindingPath[0].FieldType.GetElementType();
            if (!BaseFieldTypesSet.Contains(elementType) && !elementType.IsEnum)
            {
                elementHierarchy = GetTypeFields(elementType);
                
            }
            var parentElement = (FrameworkElement)s;
            var index = -1;
            int i = bindingPath.Count - 1;
            for (; i >= 0; i--)
            {
                if (bindingPath[i].FieldType.IsArray)
                {
                    if(elementHierarchy != null)
                    {
                        parentElement = parentElement.Parent;
                    }
                    index = (int)parentElement.Tag;
                }
                else
                {
                    parentElement = parentElement.Parent;
                }
            }

            if (index == -1) return instance;

            object parent = instance;
            
            for (i = 0; i < bindingPath.Count; i++)
            {
                parent = bindingPath[i].GetValue(parent);
                if (bindingPath[i].FieldType.IsArray)
                {
                    i++;
                    break;
                }
            }
            var array = (Array)parent;
            var current = array.GetValue(index);
            if (elementHierarchy != null)
            {
                var internalPath = bindingPath.GetRange(i, bindingPath.Count - i);
                if (internalPath.Any(e => e.FieldType.IsArray))
                {
                    current = SetValueArray(s, internalPath, current);
                }
                else
                {
                    current = SetValue(s, internalPath, current);
                }
            }
            else
            {
                current = ((VECSEditorControl)s).TryParse(current);
            }
            array.SetValue(current, index);

            for (i -= 2; i >= 0; i--)
            {
                object setter = instance;
                for (int j = 0; j < i; j++)
                {
                    setter = bindingPath[j].GetValue(setter);
                }
                bindingPath[i].SetValue(setter, current);
                current = setter;
            }

            return instance;
        }

        private static void NameFieldInstance(string name, UIElement instance)
        {
            if (instance is IEditorField customEditorField)
            {
                customEditorField.Label = name;
            }
        }


        public static void UpdateValues(EntityManager entityManager, Expander tree, int componentId, Entity entity)
        {
            IComponent component = entityManager.GetComponent(entity,componentId);

            var type = component.GetType();

            UpdateValues(entityManager, tree, GetTypeFields(type), componentId, entity);
        }

        public static void UpdateValues(EntityManager entityManager, Expander tree, FieldHierarhcy hierarhcy, int componentId, Entity entity)
        {
            IComponent component = entityManager.GetComponent(entity, componentId);

            UpdateValues(component, tree, hierarhcy);
        }

        public static void UpdateValues(object valueSrc, Expander expander)
        {
            var type = valueSrc.GetType();

            UpdateValues(valueSrc, expander, GetTypeFields(type));
        }

        public static void UpdateValues(object valueSrc, Expander tree, FieldHierarhcy hierarhcy)
        {
            for (int i = 0; i < hierarhcy.Order.Count; i++)
            {
                var orderIndices = hierarhcy.Order[i];

                if (orderIndices.X == 0)
                {
                    UpdateValues((FrameworkElement)((StackPanel)tree.Content).Children[i], hierarhcy.Types[orderIndices.Y].GetValue(valueSrc));
                }
                else
                {
                    var child = hierarhcy.Children[orderIndices.Y];
                    UpdateValues(child, (Expander)((StackPanel)tree.Content).Children[i], child.TargetSrc.GetValue(valueSrc));
                }
            }
        }


        private static void UpdateValues(FieldHierarhcy hierarhcy, Expander node, object instance)
        {
            
            for (int i = 0; i < hierarhcy.Order.Count; i++)
            {
                var orderIndices = hierarhcy.Order[i];
                
                if(orderIndices.X == 0)
                {
                    UpdateValues((FrameworkElement)((StackPanel)node.Content).Children[i], hierarhcy.Types[orderIndices.Y].GetValue(instance));
                }
                else
                {
                    var child = hierarhcy.Children[orderIndices.Y];
                    UpdateValues(child,(Expander)((StackPanel)node.Content).Children[i], child.TargetSrc.GetValue(instance));
                }
            }
        }

        private static void UpdateValues(FrameworkElement frameworkElement, object v)
        {
            Type vType = v.GetType();
            if (!TypesToControl.TryGetValue(vType, out var controlType) && !vType.IsEnum && !vType.IsArray && !vType.IsSubclassOf(typeof(Asset)))
            {
                Console.WriteLine("Invalid Control type {0} for frameworkElement {1}",vType.Name,frameworkElement.GetType().Name);
                return;
            }

            if(frameworkElement.GetType() != controlType && !vType.IsEnum && !vType.IsArray && !vType.IsSubclassOf(typeof(Asset)))
            {
                Console.WriteLine("Unexpected framework element type  {0} for control type {1}",frameworkElement.GetType().Name,controlType.Name);
                return;
            }
            if(frameworkElement is IEditorField editorField)
            {
                editorField.SetValue(v);
            }

            else if (vType.IsArray)
            {
                var arrayExpander = (Expander)frameworkElement;
                var arrayStackPanel = (StackPanel)arrayExpander.Content;
                var childCount = arrayStackPanel.Children.Count;
                var localBindingPath = (List<FieldInfo>)arrayStackPanel.Tag;
                var elementType = vType.GetElementType();
                FieldHierarhcy elementHierarchy = null;
                if (!BaseFieldTypesSet.Contains(elementType) && !elementType.IsEnum && !elementType.IsSubclassOf(typeof(Asset)))
                {
                    elementHierarchy = GetTypeFields(elementType);
                    for (int i = 0; i < elementHierarchy.BindingPaths.Count; i++)
                    {
                        elementHierarchy.BindingPaths[i] = [.. localBindingPath, .. elementHierarchy.BindingPaths[i]];
                    }
                }

                var array = (Array)v;
                if(array.Length != childCount - 1)
                {
                    arrayStackPanel.Children.Clear();
                    var countDisplay = new TextField
                    {
                        Label = "Count",
                        ValueX = array.Length.ToString(),
                        IsEnabled = false
                    };
                    arrayStackPanel.Children.Add(countDisplay);
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (elementHierarchy != null)
                        {
                            var expander = ConstructTree(elementHierarchy, localBindingPath);
                            expander.IsExpanded = false;
                            expander.Tag = -1;
                            arrayStackPanel.Children.Add(expander);
                            expander.Tag = i;
                            expander.Header = i.ToString();
                            NameFieldInstance(i.ToString(), expander);
                        }
                        else
                        {
                            FrameworkElement element;
                            if (elementType.IsSubclassOf(typeof(Asset)))
                            {
                                element = ConstructAssetRef(localBindingPath,elementType);
                            }
                            else if (elementType.IsEnum)
                            {
                                element = ConstructEnum(localBindingPath, elementType);
                            }
                            else
                            {
                                element = ConstructBaseType(localBindingPath, elementType);
                            }
                            element.Tag = i;
                            arrayStackPanel.Children.Add(element);
                            NameFieldInstance(i.ToString(), element);
                        }
                    }
                }

                for (int i = 0; i < array.Length; i++)
                {
                    var value = array.GetValue(i);
                    if (elementHierarchy != null)
                    {
                        var child = (Expander)arrayStackPanel.Children[i + 1];
                        //child.Header = value.GetType().Name;
                        UpdateValues(elementHierarchy, child, value);
                    }
                    else if (elementHierarchy == null)
                    {
                        UpdateValues((FrameworkElement)arrayStackPanel.Children[i + 1], value);
                    }
                }
            }
            else
            {
                Console.WriteLine("Valid control element type '{0}' does not implement IEditorField", frameworkElement.GetType().Name);
                
            }
            // set value somehow
        }


    }

    public class FieldHierarhcy
    {
        public Type Target;
        public FieldInfo TargetSrc;
        public List<FieldHierarhcy> Children = [];
        public List<FieldInfo> Types = [];

        public List<Vector2Int> Order = [];

        public List<List<FieldInfo>> BindingPaths;
    }
}