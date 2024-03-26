#region Using directives
using System;
using System.Linq;
using FTOptix.HMIProject;
using UAManagedCore;
using FTOptix.NetLogic;
using FTOptix.Recipe;
using FTOptix.AuditSigning;
using FTOptix.Alarm;
using FTOptix.WebUI;
using FTOptix.Core;
using FTOptix.SQLiteStore;
using FTOptix.MicroController;
using FTOptix.CommunicationDriver;
using FTOptix.RAEtherNetIP;
using FTOptix.DataLogger;
#endregion

public class ChildrenCounter : BaseNetLogic
{
    public override void Start()
    {

        var UserList = LogicObject.Owner.GetObject("UserList");
       // Clean files list
        UserList.Children.ToList().ForEach((entry) => entry.Delete());

        var UsersDetails = LogicObject.GetAlias("Users");

        foreach (var child in UsersDetails.Children.OfType<User_21CFR>())
        {
            if (child.BrowseName != "Pima")
            {

                // var User = InformationModel.MakeObject<User_21CFR>(child.BrowseName);
                UserList.Add(InformationModel.Get<User_21CFR>(child.NodeId));
            }
        }





        var nodePointerVariable = LogicObject.GetVariable("Node");
        if (nodePointerVariable == null)
        {
            Log.Error("ChildrenCounter", "Missing Node variable on ChildrenCounter");
            return;
        }

        var nodePointerValue = (NodeId) nodePointerVariable.Value;
        if (nodePointerValue == null || nodePointerValue  == NodeId.Empty)
        {
            Log.Warning("ChildrenCounter", "Node variable not set");
            return;
        }

        var countVariable = LogicObject.GetVariable("Count");
        if (countVariable == null)
        {
            Log.Error("ChildrenCounter", "Missing variable Count on ChildrenCounter");
            return;
        }

        var resolvedResult = InformationModel.Get(nodePointerValue);
        countVariable.Value = resolvedResult.Children.Count;

        if (referencesEventRegistration != null)
        {
            referencesEventRegistration.Dispose();
            referencesEventRegistration = null;
        }

        referencesObserver = new ReferencesObserver(resolvedResult, countVariable);
        referencesObserver.Initialize();

        referencesEventRegistration = resolvedResult.RegisterEventObserver(
            referencesObserver, EventType.ForwardReferenceAdded | EventType.ForwardReferenceRemoved);
    }

    public override void Stop()
    {
        if (referencesEventRegistration != null)
            referencesEventRegistration.Dispose();

        referencesEventRegistration = null;
        referencesObserver = null;
    }

    private class ReferencesObserver : IReferenceObserver
    {
        public ReferencesObserver(IUANode nodeToMonitor, IUAVariable countVariable)
        {
            this.nodeToMonitor = nodeToMonitor;
            this.countVariable = countVariable;
        }

        public void Initialize()
        {
            countVariable.Value = nodeToMonitor.Children.Count;
        }

        public void OnReferenceAdded(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            if (IsReferenceAllowed(referenceTypeId))
            {
                ++countVariable.Value;
            }
        }

        public void OnReferenceRemoved(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            if (IsReferenceAllowed(referenceTypeId) && countVariable.Value > 0)
            {
                --countVariable.Value;
            }
        }

        public bool IsReferenceAllowed(NodeId referenceTypeId)
        {
            return referenceTypeId == UAManagedCore.OpcUa.ReferenceTypes.HasComponent ||
                   referenceTypeId == UAManagedCore.OpcUa.ReferenceTypes.HasOrderedComponent;
        }

        private IUANode nodeToMonitor;
        private IUAVariable countVariable;
    }

    private ReferencesObserver referencesObserver;
    private IEventRegistration referencesEventRegistration;
}