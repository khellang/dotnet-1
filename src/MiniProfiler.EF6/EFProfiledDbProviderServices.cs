﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Spatial;
using System.Reflection;

namespace StackExchange.Profiling.Data
{
    /// <summary>
    /// Wrapper for a database provider factory to enable profiling
    /// </summary>
    /// <typeparam name="T">the factory type.</typeparam>
    public class EFProfiledDbProviderServices<T> : DbProviderServices where T : DbProviderServices
    {
#pragma warning disable RCS1158 // Avoid static members in generic types.
        /// <summary>
        /// Every provider factory must have an Instance public field
        /// </summary>
        public static readonly EFProfiledDbProviderServices<T> Instance = new EFProfiledDbProviderServices<T>();

        private readonly T _tail;

        /// <summary>
        /// Initialises a new instance of the <see cref="EFProfiledDbProviderServices{T}"/> class. 
        /// Used for DB provider APIS internally 
        /// </summary>
        /// <exception cref="Exception">Throws when the Instance is inaccessible.</exception>
        protected EFProfiledDbProviderServices()
        {
            PropertyInfo property = typeof(T).GetProperty(nameof(Instance), BindingFlags.Public | BindingFlags.Static);
            if (property != null)
                _tail = (T)property.GetValue(null, null);

            if (_tail == null)
            {
                FieldInfo field = typeof(T).GetField(nameof(Instance), BindingFlags.Public | BindingFlags.Static)
                               ?? typeof(T).GetField(nameof(Instance), BindingFlags.NonPublic | BindingFlags.Static);

                if (field != null)
                    _tail = (T)field.GetValue(null);
            }
            if (_tail == null)
            {
                throw new Exception($"Unable to define EFProfiledDbProviderServices class of type '{typeof(T).Name}'. Please check that your web.config defines a <DbProviderFactories> section underneath <system.data>.");
            }
        }

        /// <summary>
        /// Get DB command definition
        /// </summary>
        /// <param name="prototype">The prototype.</param>
        /// <returns>the command definition.</returns>
        public override DbCommandDefinition CreateCommandDefinition(DbCommand prototype) =>
            _tail.CreateCommandDefinition(prototype);

        /// <summary>
        /// The get database provider manifest.
        /// </summary>
        /// <param name="manifestToken">The manifest token.</param>
        /// <returns>the provider manifest.</returns>
        protected override DbProviderManifest GetDbProviderManifest(string manifestToken) =>
            _tail.GetProviderManifest(manifestToken);

        /// <summary>
        /// get the database provider manifest token.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>a string containing the token.</returns>
        protected override string GetDbProviderManifestToken(DbConnection connection)
        {
            if (connection is ProfiledDbConnection profiled)
            {
                connection = profiled.WrappedConnection;
            }

            return _tail.GetProviderManifestToken(connection);
        }

        /// <summary>
        /// create the database command definition.
        /// </summary>
        /// <param name="providerManifest">The provider manifest.</param>
        /// <param name="commandTree">The command tree.</param>
        /// <returns>the command definition.</returns>
        protected override DbCommandDefinition CreateDbCommandDefinition(DbProviderManifest providerManifest, DbCommandTree commandTree)
        {
            var cmdDef = _tail.CreateCommandDefinition(providerManifest, commandTree);
            var cmd = cmdDef.CreateCommand();
            return CreateCommandDefinition(new ProfiledDbCommand(cmd, cmd.Connection, MiniProfiler.Current));
        }

        /// <summary>
        /// create the database.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="commandTimeout">The command timeout.</param>
        /// <param name="storeItemCollection">The store item collection.</param>
        protected override void DbCreateDatabase(DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection) =>
            _tail.CreateDatabase(GetRealConnection(connection), commandTimeout, storeItemCollection);

        /// <summary>
        /// delete the database.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="commandTimeout">The command timeout.</param>
        /// <param name="storeItemCollection">The store item collection.</param>
        protected override void DbDeleteDatabase(DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection) =>
            _tail.DeleteDatabase(GetRealConnection(connection), commandTimeout, storeItemCollection);

        /// <summary>
        /// create the database script.
        /// </summary>
        /// <param name="providerManifestToken">The provider manifest token.</param>
        /// <param name="storeItemCollection">The store item collection.</param>
        /// <returns>a string containing the database script.</returns>
        protected override string DbCreateDatabaseScript(string providerManifestToken, StoreItemCollection storeItemCollection) =>
            _tail.CreateDatabaseScript(providerManifestToken, storeItemCollection);

        /// <summary>
        /// test if the database exists.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="commandTimeout">The command timeout.</param>
        /// <param name="storeItemCollection">The store item collection.</param>
        /// <returns>true if the database exists.</returns>
        protected override bool DbDatabaseExists(DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection) =>
            _tail.DatabaseExists(GetRealConnection(connection), commandTimeout, storeItemCollection);

        /// <summary>
        /// Gets the real connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>the database connection</returns>
        private static DbConnection GetRealConnection(DbConnection connection) =>
            connection is ProfiledDbConnection profiled ? profiled.WrappedConnection : connection;

        private static DbDataReader GetSpatialDataReader(DbDataReader fromReader) =>
            fromReader is ProfiledDbDataReader profiled ? profiled.WrappedReader : fromReader;

        /// <summary>
        /// Called to resolve additional default provider services when a derived type is registered 
        /// as an EF provider either using an entry in the application's config file or through code-based 
        /// registration in <c>DbConfiguration</c>. The implementation of this method in this class uses the resolvers 
        /// added with the AddDependencyResolver method to resolve dependencies.
        /// </summary>
        /// <param name="type">The type of the service to be resolved.</param>
        /// <param name="key">An optional key providing additional information for resolving the service.</param>
        /// <returns>An instance of the given type, or null if the service could not be resolved.</returns>
        public override object GetService(Type type, object key) =>
            _tail.GetService(type, key);

        /// <summary>
        /// Called to resolve additional default provider services when a derived type is registered 
        /// as an EF provider either using an entry in the application's config file or through code-based 
        /// registration in <c>DbConfiguration</c>. The implementation of this method in this class uses the resolvers 
        /// added with the AddDependencyResolver method to resolve dependencies.
        /// </summary>
        /// <param name="type">The type of the service to be resolved.</param>
        /// <param name="key">An optional key providing additional information for resolving the service.</param>
        /// <returns>All registered services that satisfy the given type and key, or an empty enumeration if there are none.</returns>
        public override IEnumerable<object> GetServices(Type type, object key) =>
            _tail.GetServices(type, key);

        /// <summary>
        /// Gets the spatial data reader for the DbProviderServices.
        /// </summary>
        /// <param name="fromReader">The reader where the spatial data came from.</param>
        /// <param name="manifestToken">The token information associated with the provider manifest.</param>
        /// <returns>The spatial data reader.</returns>
        protected override DbSpatialDataReader GetDbSpatialDataReader(DbDataReader fromReader, string manifestToken)
        {
            var setDbParameterValueMethod = Array.Find(_tail.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic), f => f.Name.Equals("GetDbSpatialDataReader"));
            var reader = GetSpatialDataReader(fromReader);

            if (setDbParameterValueMethod == null)
            {
                return base.GetDbSpatialDataReader(reader, manifestToken);
            }

            var result = setDbParameterValueMethod.Invoke(_tail, new object[] { reader, manifestToken });
            return result as DbSpatialDataReader;
        }

        /// <summary>
        /// Gets the spatial services for the <c>DbProviderServices</c>.
        /// </summary>
        /// <param name="manifestToken">The token information associated with the provider manifest.</param>
        /// <returns>The spatial services.</returns>
        [Obsolete("Return DbSpatialServices from the GetService method. See http://go.microsoft.com/fwlink/?LinkId=260882 for more information.")]
        protected override DbSpatialServices DbGetSpatialServices(string manifestToken)
        {
            var dbGetSpatialServices = Array.Find(_tail.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic), f => f.Name.Equals("DbGetSpatialServices"));
            if (dbGetSpatialServices != null) return dbGetSpatialServices.Invoke(_tail, new[] { manifestToken }) as DbSpatialServices;
            return null;
        }

        /// <summary>
        /// Sets the parameter value and appropriate facets for the given <c>TypeUsage</c>.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        /// <param name="parameterType">The type of parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        protected override void SetDbParameterValue(DbParameter parameter, TypeUsage parameterType, object value)
        {
            // if this is available in _tail, use it
            var setDbParameterValueMethod = Array.Find(_tail.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic), f => f.Name.Equals("SetDbParameterValue"));
            if (setDbParameterValueMethod != null)
            {
                setDbParameterValueMethod.Invoke(_tail, new[] { parameter, parameterType, value });
                return;
            }

            // this should never need to be called, but just in case, get the Provider Value
            if (value is DbGeography pvalue)
            {
                value = pvalue.ProviderValue;
            }
            base.SetDbParameterValue(parameter, parameterType, value);
        }
    }
}
