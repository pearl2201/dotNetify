﻿/*
Copyright 2016-2020 Dicky Suryadi

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetify.Security;
using DotNetify.Util;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;

namespace DotNetify
{
   /// <summary>
   /// Interface for enabling compile-time check for public hub methods. Not meant to be implemented!
   /// </summary>
   public interface IDotNetifyHubMethod
   {
      void Request_VM();

      void Update_VM();

      void Dispose_VM();

      void Response_VM();

      void Invoke();
   }

   /// <summary>
   /// This class is a SignalR hub for communicating with browser clients.
   /// </summary>
   public class DotNetifyHub : Hub
   {
      private readonly IVMControllerFactory _vmControllerFactory;
      private readonly IHubServiceProvider _serviceProvider;
      private readonly IPrincipalAccessor _principalAccessor;
      private readonly IHubPipeline _hubPipeline;
      private readonly IHubContext<DotNetifyHub> _globalHubContext;
      private IDotNetifyHubHandler _hubHandler;

      /// <summary>
      /// Handles hub methods.
      /// </summary>
      protected internal IDotNetifyHubHandler HubHandler
      {
         get
         {
            _hubHandler = _hubHandler ?? GetHubHandler(new DotNetifyHubResponse(_globalHubContext));
            return _hubHandler;
         }
         set => _hubHandler = value;
      }

      /// <summary>
      /// Constructor for dependency injection.
      /// </summary>
      /// <param name="vmControllerFactory">Factory of view model controllers.</param>
      /// <param name="serviceProvider">Allows to provide scoped service provider for the view models.</param>
      /// <param name="principalAccessor">Allows to pass the hub principal.</param>
      /// <param name="hubPipeline">Manages middlewares and view model filters.</param>
      /// <param name="globalHubContext">Provides access to hubs.</param>
      public DotNetifyHub(
         IVMControllerFactory vmControllerFactory,
         IHubServiceProvider serviceProvider,
         IPrincipalAccessor principalAccessor,
         IHubPipeline hubPipeline,
         IHubContext<DotNetifyHub> globalHubContext)
      {
         _vmControllerFactory = vmControllerFactory;
         _serviceProvider = serviceProvider;
         _principalAccessor = principalAccessor;
         _hubPipeline = hubPipeline;
         _globalHubContext = globalHubContext;
      }

      /// <summary>
      /// Handles when a client gets disconnected.
      /// </summary>
      public override async Task OnDisconnectedAsync(Exception exception)
      {
         await HubHandler.OnDisconnectedAsync(exception);
         await base.OnDisconnectedAsync(exception);
      }

      /// <summary>
      /// This method is called by browser clients to request view model data.
      /// </summary>
      /// <param name="vmId">Identifies the view model.</param>
      /// <param name="vmArg">Optional argument that may contain view model's initialization argument and/or request headers.</param>
      [HubMethodName(nameof(IDotNetifyHubMethod.Request_VM))]
      public async Task RequestVMAsync(string vmId, object vmArg)
      {
         await HubHandler.RequestVMAsync(vmId, vmArg);
      }

      /// <summary>
      /// This method is called by browser clients to update a view model's value.
      /// </summary>
      /// <param name="vmId">Identifies the view model.</param>
      /// <param name="vmData">View model update data, where key is the property path and value is the property's new value.</param>
      [HubMethodName(nameof(IDotNetifyHubMethod.Update_VM))]
      public async Task UpdateVMAsync(string vmId, Dictionary<string, object> vmData)
      {
         await HubHandler.UpdateVMAsync(vmId, vmData);
      }

      /// <summary>
      /// This method is called by browser clients to remove its view model as it's no longer used.
      /// </summary>
      /// <param name="vmId">Identifies the view model.  By convention, this should match a view model class name.</param>
      [HubMethodName(nameof(IDotNetifyHubMethod.Dispose_VM))]
      public async Task DisposeVMAsyc(string vmId)
      {
         await HubHandler.DisposeVMAsync(vmId);
      }

      /// <summary>
      /// This method is called by the hub server to forward messages to another server.
      /// </summary>
      /// <param name="methodName">Method name to invoke.</param>
      /// <param name="methodArgs">Method arguments.</param>
      /// <param name="metadata">Any metadata.</param>
      [HubMethodName(nameof(IDotNetifyHubMethod.Invoke))]
      public async Task InvokeAsync(string methodName, object[] methodArgs, IDictionary<string, object> metadata)
      {
         foreach (var kvp in metadata)
            Context.Items[kvp.Key] = kvp.Value;

         switch (methodName)
         {
            case nameof(IDotNetifyHubMethod.Request_VM):
               methodName = nameof(RequestVMAsync);
               break;

            case nameof(IDotNetifyHubMethod.Update_VM):
               methodName = nameof(UpdateVMAsync);
               break;

            case nameof(IDotNetifyHubMethod.Dispose_VM):
               methodName = nameof(DisposeVMAsyc);
               break;
         }

         var methodInfo = GetType().GetMethod(methodName);
         var methodParams = methodInfo.GetParameters();

         for (int i = 0; i < methodArgs.Length; i++)
            methodArgs[i] = methodArgs[i].ToString().ConvertFromString(methodParams[i].ParameterType);

         HubHandler = GetHubHandler(new DotNetifyHubForwardResponse(_globalHubContext, Context));
         await methodInfo.InvokeAsync(this, methodArgs);
      }

      /// <summary>
      /// Returns handler to hub messages.
      /// </summary>
      /// <param name="hubResponse">Handles the message response.</param>
      private IDotNetifyHubHandler GetHubHandler(IDotNetifyHubResponse hubResponse)
      {
         return new DotNetifyHubHandler(_vmControllerFactory, _serviceProvider, _principalAccessor, _hubPipeline, hubResponse, Context);
      }

      #region Obsolete Methods

      [Obsolete]
      [HubMethodName("Request_VM_Obsolete")]
      public void Request_VM(string vmId, object vmArg) => _ = RequestVMAsync(vmId, vmArg);

      [Obsolete]
      [HubMethodName("Update_VM_Obsolete")]
      public void Update_VM(string vmId, Dictionary<string, object> vmData) => _ = UpdateVMAsync(vmId, vmData);

      [Obsolete]
      [HubMethodName("Dispose_VM_Obsolete")]
      public void Dispose_VM(string vmId) => _ = DisposeVMAsyc(vmId);

      #endregion Obsolete Methods
   }
}