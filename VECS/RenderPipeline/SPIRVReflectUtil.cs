using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using VECS.GraphicsPipelines;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public static class SPIRVReflectUtil
    {
        public static unsafe SpvReflectShaderModule CreateReflectShaderModule(byte[] spirv_code)
        {
            SpvReflectShaderModule module;
            byte[] shaderBytes = spirv_code;
            SpvReflectResult result = SPIRVReflectApi.spvReflectCreateShaderModule(shaderBytes, &module);
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to create reflected shader module: {0}", result.ToString()));
            }
            return module;
        }

        public static unsafe void DestroyReflectShaderModule(SpvReflectShaderModule module)
        {
            SPIRVReflectApi.spvReflectDestroyShaderModule(&module);
        }

        public static unsafe void SpirvReflectShaderInfo(byte[] spirv_code)
        {
            SpvReflectShaderModule module = CreateReflectShaderModule(spirv_code);
            
            var inputVars = EnumerateInputVariables(module);
            var outputVars = EnumerateOutputVariables(module);
            var pushConstants = PushConstants(module);
            var descriptorBindings = DescriptorBindings(module);
            var descriptorSets = DescriptorSets(module);

            if (inputVars != null)
            {
                Console.WriteLine("In Vars");
                for (int i = 0; i < inputVars.Length; i++)
                {
                    Console.WriteLine("{0} {1}", inputVars[i].built_in, inputVars[i].Name);
                }
            }
            if (outputVars != null)
            {
                Console.WriteLine("Out Vars");
                for (int i = 0; i < outputVars.Length; i++)
                {
                    Console.WriteLine("{0} {1}", outputVars[i].location, outputVars[i].Name);
                }
            }
            if (pushConstants != null)
            {
                Console.WriteLine("Push Constants");
                for (int i = 0; i < pushConstants.Length; i++)
                {
                    Console.WriteLine("{0} {1}", pushConstants[i].size, pushConstants[i].Name);
                    for (int j = 0; j < pushConstants[i].member_count; j++)
                    {
                        var member = pushConstants[i].members[j];
                        Console.WriteLine("{0} {1}", member.size, member.Name);
                    }
                }
            }
            if (descriptorBindings != null)
            {
                Console.WriteLine("Descriptor Bindings");
                for(int i = 0;i < descriptorBindings.Length; i++)
                {
                    Console.WriteLine("{0} {1}", descriptorBindings[i].binding, descriptorBindings[i].Name);
                }
            }
            if(descriptorSets != null)
            {
                Console.WriteLine("Descriptor Sets");
                for (int i = 0; i < descriptorSets.Length; i++)
                {
                    Console.WriteLine("{0} {1}", descriptorSets[i].set, descriptorSets[i].binding_count);
                }
            }
            DestroyReflectShaderModule(module);
        }

        public unsafe static SpvReflectInterfaceVariable[] EnumerateInputVariables(SpvReflectShaderModule module)
        {
            uint var_count = 0;

            var result = SPIRVReflectApi.spvReflectEnumerateInputVariables(&module, &var_count, null);
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate input variables: {0}", result.ToString()));
            }
            if (var_count == 0) return null;

            SpvReflectInterfaceVariable[] variables = new SpvReflectInterfaceVariable[var_count];
            SpvReflectInterfaceVariable** input_vars = (SpvReflectInterfaceVariable**)NativeMemory.AllocZeroed((nuint)(var_count * sizeof(SpvReflectInterfaceVariable*)));

            result = SPIRVReflectApi.spvReflectEnumerateInputVariables(&module, &var_count, input_vars);

            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate input variables: {0}", result.ToString()));
            }

            for (int i = 0; i < var_count; i++)
            {
                variables[i] = *input_vars[i];
            }

            NativeMemory.Free(input_vars);

            return variables;
        }

        public unsafe static SpvReflectInterfaceVariable[] EnumerateOutputVariables(SpvReflectShaderModule module)
        {
            uint var_count = 0;

            var result = SPIRVReflectApi.spvReflectEnumerateOutputVariables(&module, &var_count, null);
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate input variables: {0}", result.ToString()));
            }
            if (var_count == 0) return null;
            
            SpvReflectInterfaceVariable[] variables = new SpvReflectInterfaceVariable[var_count];

            SpvReflectInterfaceVariable** pOutputVars = (SpvReflectInterfaceVariable**)NativeMemory.AllocZeroed((nuint)(var_count * sizeof(SpvReflectInterfaceVariable*)));

            result = SPIRVReflectApi.spvReflectEnumerateOutputVariables(&module, &var_count, pOutputVars);

            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate input variables: {0}", result.ToString()));
            }

            for (int i = 0; i < var_count; i++)
            {
                variables[i] = *pOutputVars[i];
            }

            NativeMemory.Free(pOutputVars);

            return variables;
        }

        public unsafe static SpvReflectBlockVariable[] PushConstants(SpvReflectShaderModule module)
        {
            uint block_count = 0;
            var result = SPIRVReflectApi.spvReflectEnumeratePushConstantBlocks(&module, &block_count, null);
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate push constant blocks: {0}", result.ToString()));
            }
            if (block_count == 0) return null;
            
            SpvReflectBlockVariable[] pushConstants = new SpvReflectBlockVariable[block_count];

            SpvReflectBlockVariable** pPushConstants = (SpvReflectBlockVariable**)NativeMemory.Alloc((nuint)(block_count * sizeof(SpvReflectBlockVariable)));
            
            result = SPIRVReflectApi.spvReflectEnumeratePushConstantBlocks(&module, &block_count, pPushConstants); if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate push constant blocks: {0}", result.ToString()));
            }

            for (int i = 0; i < block_count; i++)
            {
                pushConstants[i] = *pPushConstants[i];
            }

            NativeMemory.Free(pPushConstants);

            return pushConstants;
        }

        public unsafe static SpvReflectDescriptorBinding[] DescriptorBindings(SpvReflectShaderModule module)
        {
            uint binding_count = 0;
            var result = SPIRVReflectApi.spvReflectEnumerateDescriptorBindings(&module, &binding_count, null);
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate descriptor bindings: {0}", result.ToString()));
            }
            if (binding_count == 0) return null;
            
            SpvReflectDescriptorBinding[] bindings = new SpvReflectDescriptorBinding[binding_count];

            SpvReflectDescriptorBinding** pDescriptorBindings = (SpvReflectDescriptorBinding**)NativeMemory.AllocZeroed((nuint)(binding_count * sizeof(SpvReflectBlockVariable*)));
            
            result = SPIRVReflectApi.spvReflectEnumerateDescriptorBindings(&module, &binding_count, pDescriptorBindings);
            
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate descriptor bindings: {0}", result.ToString()));
            }

            for (int i = 0; i < binding_count; i++)
            {
                bindings[i] = *pDescriptorBindings[i];
            }

            NativeMemory.Free(pDescriptorBindings);

            return bindings;
        }

        public unsafe static SpvReflectDescriptorSet[] DescriptorSets(SpvReflectShaderModule module)
        {
            uint set_count = 0;
            var result = SPIRVReflectApi.spvReflectEnumerateDescriptorSets(&module, &set_count, null);
            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate descriptor sets: {0}", result.ToString()));
            }
            if (set_count == 0) return null;
            
            SpvReflectDescriptorSet[] sets = new SpvReflectDescriptorSet[set_count];

            SpvReflectDescriptorSet** pDescriptorSets = (SpvReflectDescriptorSet**)NativeMemory.AllocZeroed((nuint)(set_count * sizeof(SpvReflectDescriptorSet*)));

            result = SPIRVReflectApi.spvReflectEnumerateDescriptorSets(&module, &set_count, pDescriptorSets);

            if (result != SpvReflectResult.Success)
            {
                throw new Exception(string.Format("Failed to enumerate descriptor bindings: {0}", result.ToString()));
            }

            for (int i = 0; i < set_count; i++)
            {
                sets[i] = *pDescriptorSets[i];
            }

            NativeMemory.Free(pDescriptorSets);

            return sets;
        }
    }
}
