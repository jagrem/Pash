﻿using System;
using NUnit.Framework;
using System.Management.Automation;
using TestPSSnapIn;
using System.Runtime.Remoting;
using System.Collections.Generic;

namespace ReferenceTests.Language
{
    [TestFixture]
    public class ObjectMemberTests : ReferenceTestBase
    {
        [Test]
        public void CustomPSObjectPropertiesCanBeAccessedCaseInsensitive()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = New-Object -Type PSObject",
                "$a | Add-Member -Type NoteProperty -Name TestName -Value TestValue",
                "$a.testname"
            ));
            Assert.AreEqual(NewlineJoin("TestValue"), result);
        }

        [Test]
        public void AccessingNonExistingPropertiesDoesntFail()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = New-Object -Type PSObject",
                "$a.testname"
            ));
            Assert.AreEqual(NewlineJoin(), result);
        }

        [Test]
        public void CanGetCustomCSharpObjectAndIdentifyType()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)),
                "$a.GetType().FullName"
            ));
            Assert.AreEqual(NewlineJoin(typeof(CustomTestClass).FullName), result);
        }

        [Test]
        public void CanAccessCSharpObjectProperty()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$a.MessageProperty"
            ));
            Assert.AreEqual(NewlineJoin("foo"), result);
        }

        [Test]
        public void CanSetCSharpObjectProperty()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$a.MessageProperty = 'baz'",
                "$a.MessageProperty"
            ));
            Assert.AreEqual(NewlineJoin("baz"), result);
        }

        [Test]
        public void CanAccessCSharpObjectField()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$a.MessageField"
            ));
            Assert.AreEqual(NewlineJoin("bar"), result);
        }

        [Test]
        public void CanSetCSharpObjectField()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$a.MessageField = 'baz'",
                "$a.MessageField"
            ));
            Assert.AreEqual(NewlineJoin("baz"), result);
        }

        [Test]
        public void CanInvokeCSharpObjectMethodAndGetResult()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$b = $a.Combine()",
                "$b.GetType().FullName",
                "$b"
            ));
            var expected = NewlineJoin(typeof(string).FullName, "foobar");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void CanInvokeCSharpObjectMethodWithArguments()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$a.SetMessages('bla', 'blub')",
                "$a.MessageProperty",
                "$a.MessageField"
            ));
            var expected = NewlineJoin("bla", "blub");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void AccessingMemberOfNullThrows()
        {
            // TODO: check exception type
            Assert.Throws(Is.InstanceOf(typeof(Exception)), delegate {
                ReferenceHost.Execute("$a.Bar = 0");
            });
        }

        [Test]
        public void InvokingMemberOfNullThrows()
        {
            // TODO: check exception type
            Assert.Throws(Is.InstanceOf(typeof(Exception)), delegate {
                ReferenceHost.Execute("$a.GetType()");
            });
        }

        [Test]
        public void AccessingMemberOfNullDoesntThrow()
        {
            Assert.DoesNotThrow(delegate {
                ReferenceHost.Execute("$null.Foo; $a.Bar");
            });
        }

        [Test]
        public void CanGetCSharpObjectMethodAndInvokeLater()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = " + CmdletName(typeof(TestCreateCustomObjectCommand)) + " 'foo' 'bar'",
                "$b = $a.SetMessages",
                "$c = $a.Combine",
                "$b.Invoke('bla', 'blub')",
                "$c.Invoke()"
            ));
            Assert.AreEqual(NewlineJoin("blablub"), result);
        }

        [Test]
        public void CanInvokeMethodWithNullArg()
        {
            ExecuteAndCompareTypedResult("[string]::IsNullOrEmpty($null)", true);
        }

        [Test]
        public void PSObjectIsntCopiedAndPropertyIsUpdatable()
        {
            var result = ReferenceHost.Execute(NewlineJoin(
                "$a = new-object psobject -property @{foo='a';bar='b';baz='c'}",
                "$b = $a",
                "$a.baz",
                "$b.baz",
                "$b.baz='d'",
                "$a.baz",
                "$b.baz"
            ));
            Assert.AreEqual(NewlineJoin("c", "c", "d", "d"), result);
        }
    }
}

