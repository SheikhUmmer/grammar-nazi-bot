﻿using Firebase.Database;
using Firebase.Database.Query;
using GrammarNazi.Domain.Repositories;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GrammarNazi.Core.Repositories
{
    public class FirebaseRepository<T> : IRepository<T> where T : class
    {
        private readonly FirebaseClient _firebaseClient;

        public FirebaseRepository(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient;
        }

        public async Task Add(T entity)
        {
            await ExecuteFirebaseQuery(() => _firebaseClient.Child(typeof(T).Name).PostAsync(JsonConvert.SerializeObject(entity)));
        }

        public async Task<bool> Any(Expression<Func<T, bool>> filter = default)
        {
            if (filter == default)
            {
                var items = await ExecuteFirebaseQuery(() => _firebaseClient.Child(typeof(T).Name).OrderByKey().LimitToFirst(1).OnceAsync<T>());
                return items.Count > 0;
            }

            var results = await GetAllItems();

            return results.Any(filter.Compile());
        }

        public async Task Delete(T entity)
        {
            var results = await _firebaseClient.Child(typeof(T).Name).OnceAsync<T>();

            var firebaseObject = results.FirstOrDefault(v => v.Object.Equals(entity));

            if (firebaseObject != default)
            {
                await ExecuteFirebaseQuery(() => _firebaseClient.Child(typeof(T).Name).Child(firebaseObject.Key).DeleteAsync());
            }
        }

        public async Task<T> GetFirst(Expression<Func<T, bool>> filter)
        {
            var results = await GetAllItems();

            return results.FirstOrDefault(filter.Compile());
        }

        public async Task<TResult> Max<TResult>(Expression<Func<T, TResult>> selector)
        {
            var results = await GetAllItems();

            return results.Max(selector.Compile());
        }

        public async Task Update(T entity, Expression<Func<T, bool>> identifier)
        {
            var results = await ExecuteFirebaseQuery(() => _firebaseClient.Child(typeof(T).Name).OnceAsync<T>());

            var firebaseObject = results.FirstOrDefault(v => v.Object.Equals(entity));

            if (firebaseObject == default)
            {
                throw new InvalidOperationException($"Firebase object not found. {typeof(T).Name}:{entity}");
            }

            await ExecuteFirebaseQuery(() => _firebaseClient.Child(typeof(T).Name).Child(firebaseObject.Key).PutAsync(JsonConvert.SerializeObject(entity)));
        }

        private async Task<IEnumerable<T>> GetAllItems()
        {
            var allItems = await ExecuteFirebaseQuery(() => _firebaseClient.Child(typeof(T).Name).OnceAsync<T>());

            return allItems.Select(v => v.Object);
        }

        public async Task<IEnumerable<T>> GetAll(Expression<Func<T, bool>> filter)
        {
            var items = await GetAllItems();

            return items.Where(filter.Compile());
        }

        private async Task<TResult> ExecuteFirebaseQuery<TResult>(Func<Task<TResult>> action, [CallerMemberName] string callerMemberName = "")
        {
            try
            {
                return await action();
            }
            catch (FirebaseException ex)
            {
                throw new InvalidOperationException($"Error on {callerMemberName}", ex);
            }
        }

        private Task ExecuteFirebaseQuery(Func<Task> action, [CallerMemberName] string callerMemberName = "")
        {
            return ExecuteFirebaseQuery(async () =>
            {
                await action();
                return 0;
            }, callerMemberName);
        }
    }