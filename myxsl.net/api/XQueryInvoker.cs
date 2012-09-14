﻿// Copyright 2012 Max Toro Q.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Web.Compilation;
using System.Web.Routing;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using myxsl.net.web.ui;
using myxsl.net.common;
using CacheByProcessor = System.Collections.Concurrent.ConcurrentDictionary<myxsl.net.common.IXQueryProcessor, System.Collections.Concurrent.ConcurrentDictionary<System.Uri, myxsl.net.common.XQueryExecutable>>;

namespace myxsl.net {
   
   public class XQueryInvoker {

      static readonly CacheByProcessor cacheByProc = new CacheByProcessor();
      
      readonly XQueryExecutable executable;
      readonly XmlResolver resolver;

      public static XQueryInvoker With(string queryUri) {
         return With(queryUri, (IXQueryProcessor)null, Assembly.GetCallingAssembly());
      }

      public static XQueryInvoker With(string queryUri, string processor) {
         return With(queryUri, Processors.XQuery[processor], Assembly.GetCallingAssembly());
      }

      public static XQueryInvoker With(string queryUri, IXQueryProcessor processor) {
         return With(queryUri, processor, Assembly.GetCallingAssembly());
      }

      static XQueryInvoker With(string queryUri, IXQueryProcessor processor, Assembly callingAssembly) {
         return With(new Uri(queryUri, UriKind.RelativeOrAbsolute), processor, callingAssembly);
      }

      public static XQueryInvoker With(Uri queryUri) {
         return With(queryUri, (IXQueryProcessor)null, Assembly.GetCallingAssembly());
      }

      public static XQueryInvoker With(Uri queryUri, string processor) {
         return With(queryUri, Processors.XQuery[processor], Assembly.GetCallingAssembly());
      }

      public static XQueryInvoker With(Uri queryUri, IXQueryProcessor processor) {
         return With(queryUri, processor, Assembly.GetCallingAssembly());
      }

      static XQueryInvoker With(Uri queryUri, IXQueryProcessor processor, Assembly callingAssembly) {

         if (queryUri == null) throw new ArgumentNullException("queryUri");

         var resolver = new XmlDynamicResolver(callingAssembly);
         
         string originalUri = queryUri.OriginalString;

         if (!queryUri.IsAbsoluteUri)
            queryUri = resolver.ResolveUri(null, originalUri);

         if (processor == null)
            processor = Processors.XQuery.DefaultProcessor;

         ConcurrentDictionary<Uri, XQueryExecutable> cache =
            cacheByProc.GetOrAdd(processor, p => new ConcurrentDictionary<Uri, XQueryExecutable>());

         XQueryExecutable executable = cache.GetOrAdd(queryUri, u => {

            XQueryExecutable exec = null;

            if (originalUri[0] == '~') {
               XQueryPage page = BuildManager.CreateInstanceFromVirtualPath(originalUri, typeof(object)) as XQueryPage;

               if (page != null
                  && processor.GetType() == page.Executable.Processor.GetType()) {
                  
                  exec = page.Executable;
               }
            }

            if (exec == null) {

               using (var stylesheetSource = (Stream)resolver.GetEntity(queryUri, null, typeof(Stream))) {
                  exec = processor.Compile(stylesheetSource, new XQueryCompileOptions {
                     BaseUri = queryUri,
                     XmlResolver = resolver
                  });
               }
            }

            return exec;
         });

         return new XQueryInvoker(executable, resolver);
      }

      private XQueryInvoker(XQueryExecutable executable, XmlResolver resolver) {
         
         this.executable = executable;
         this.resolver = resolver;
      }

      public XQueryResultHandler Query(Stream input) {
         return Query(input, null);
      }

      public XQueryResultHandler Query(Stream input, object parameters) {

         IXPathNavigable doc = this.executable.Processor.ItemFactory
            .CreateNodeReadOnly(input, new XmlParsingOptions {
               XmlResolver = this.resolver
            });

         return Query(doc, parameters);
      }

      public XQueryResultHandler Query(TextReader input) {
         return Query(input, null);
      }

      public XQueryResultHandler Query(TextReader input, object parameters) {

         IXPathNavigable doc = this.executable.Processor.ItemFactory
            .CreateNodeReadOnly(input, new XmlParsingOptions {
               XmlResolver = this.resolver
            });

         return Query(doc, parameters);
      }

      public XQueryResultHandler Query(XmlReader input) {
         return Query(input, null);
      }

      public XQueryResultHandler Query(XmlReader input, object parameters) {

         IXPathNavigable doc = this.executable.Processor.ItemFactory.CreateNodeReadOnly(input);

         return Query(doc, parameters);
      }

      public XQueryResultHandler Query(IXPathNavigable input) {
         return Query(input, null);
      }

      public XQueryResultHandler Query(IXPathNavigable input, object parameters) {

         if (input == null) throw new ArgumentNullException("input");

         var options = new XQueryRuntimeOptions {
            ContextItem = input,
            InputXmlResolver = this.resolver
         };

         if (parameters != null) {
            var paramDictionary = new RouteValueDictionary(parameters);

            foreach (var pair in paramDictionary)
               options.ExternalVariables.Add(new XmlQualifiedName(pair.Key), pair.Value);
         }

         return Query(options);
      }

      public XQueryResultHandler Query(object input) {
         return Query(input, null);
      }

      public XQueryResultHandler Query(object input, object parameters) {
         return Query(this.executable.Processor.ItemFactory.CreateDocument(input), parameters);
      }

      public XQueryResultHandler Query(XQueryRuntimeOptions options) {
         return new XQueryResultHandler(this.executable, options);
      }
   }
}
