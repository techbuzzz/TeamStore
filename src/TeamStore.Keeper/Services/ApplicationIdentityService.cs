﻿namespace TeamStore.Keeper.Services
{
    using System.Security.Principal;
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using Microsoft.EntityFrameworkCore;
    using System.Threading.Tasks;
    using TeamStore.Keeper.Interfaces;
    using TeamStore.Keeper.DataAccess;
    using TeamStore.Keeper.Models;
    using TeamStore.Keeper.Factories;
    using System.Linq.Expressions;

    // NOTE: I am rethinking the dependency on HttpContext. The idea is to "get the current user" at 
    // different life cycle events.

    /// <summary>
    /// This service is responsible for retrieving and creating Application Identities (users or groups)
    /// </summary>
    public class ApplicationIdentityService : IApplicationIdentityService
    {
        public const string CURRENTUSERKEY = "Auth_CurrentUser";

        private ApplicationDbContext _dbContext;
        private HttpContext _httpContext;
        private IDictionary<object, object> _itemsCollection;
        private IGraphService _graphService;

        private bool? _isCurrentUserAdmin;

        /// <summary>
        /// Constructor for the ApplicationIdentityService
        /// </summary>
        /// <param name="context">The database context with <see cref="ApplicationIdentity"/> entities</param>
        /// <param name="httpContextAccessor">The ASP.NET Core IHttpContextAccessor to reference a current HttpContext</param>
        /// <param name="itemsCollection">An items collection override to retrieve per-request instantiated users</param>
        public ApplicationIdentityService(
            ApplicationDbContext context,
            IGraphService graphService,
            IHttpContextAccessor httpContextAccessor,
            IDictionary<object, object> itemsCollection = null)
        {
            _dbContext = context ?? throw new ArgumentNullException(nameof(context));
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));

            // we depend on an HttpContext
            _httpContext = httpContextAccessor?.HttpContext ?? throw new ArgumentNullException(nameof(_httpContext));

            // Set the items colletion from the context
            if (_httpContext.Items != null && itemsCollection == null)
            {
                _itemsCollection = _httpContext.Items;
            }
            else // if no items in context, set from passed values
            {
                _itemsCollection = itemsCollection;
            }
        }

        /// <summary>
        /// Retrieves the current logged in user Application Identity
        /// </summary>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        public async Task<ApplicationUser> GetCurrentUser()
        {
            if (_httpContext == null) return null;
            if (_itemsCollection == null) return null;
            //if (_itemsCollection.ContainsKey(CURRENTUSERKEY) == false) return null;

            // 1. Check context Item for Application User
            if (_itemsCollection.ContainsKey(CURRENTUSERKEY) &&
                _itemsCollection[CURRENTUSERKEY] != null &&
                _itemsCollection[CURRENTUSERKEY] is ApplicationUser applicationUser)
                return applicationUser;

            // 2. return from HttpContext.User if the context item collection does not have it
            return await GetCurrentUser(_httpContext.User?.Identity);
        }

        /// <summary>
        /// Creates or returns the <see cref="ApplicationUser"/> representing a passed IIdentity.
        /// This method checks the database first.
        /// </summary>
        /// <param name="identity">The IIdentity to resolve and map to an <see cref="ApplicationUser"/></param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        public async Task<ApplicationUser> GetCurrentUser(IIdentity identity)
        {
            if (identity == null) return null;
            if (identity.IsAuthenticated == false) return null; // not authenticated, so we shouldn't build an object

            var existingUser = await FindUserAsync(identity as ClaimsIdentity); // get from database first
            ApplicationUser currentApplicationUser;
            if (existingUser != null)
            {
                currentApplicationUser = existingUser;
            }
            else
            {
                // Creates a new ApplicationUser object, watch out for duplication of entities in the DB
                currentApplicationUser = UserIdentityFactory.CreateNewApplicationUserFromAzureIdentity(identity as ClaimsIdentity);
            }

            // Set it in the HttpContext items collection for the current request
            UpdateUserInItemsCollection(currentApplicationUser);

            return currentApplicationUser;
        }

        /// <summary>
        /// Sets the passed <see cref="ApplicationUser"/> as the current logged in user in the items collection
        /// </summary>
        /// <param name="currentApplicationUser">The <see cref="ApplicationUser"/> object to set as the current user</param>
        private void UpdateUserInItemsCollection(ApplicationUser currentApplicationUser)
        {
            // Update the HttpContext requet object if it is not set. On the next Get it will get it from the context.
            if (_itemsCollection[CURRENTUSERKEY] != null && _itemsCollection[CURRENTUSERKEY] as ApplicationUser != currentApplicationUser)
            {
                _itemsCollection[CURRENTUSERKEY] = currentApplicationUser;
            }

            // Set the context if it is empty
            if (_itemsCollection[CURRENTUSERKEY] == null)
            {
                _itemsCollection[CURRENTUSERKEY] = currentApplicationUser;
            }
        }

        /// <summary>
        /// Retrieves an <see cref="ApplicationUser"/> from the database by looking up the 
        /// passed ClaimsIdentity object. Matches a user by the object identifier claim within the ClaimsIdentity 
        /// claims collection.
        /// </summary>
        /// <param name="identity">The ClaimsIdentity holding the object identifier claim to lookup.</param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        public async Task<ApplicationUser> FindUserAsync(ClaimsIdentity identity)
        {
            var claim = identity.Claims.FirstOrDefault(c => c.Type == Constants.CLAIMS_OBJECTIDENTIFIER);
            if (claim == null) return null;
            if (string.IsNullOrWhiteSpace(claim.Value)) return null;

            return await FindUserAsync(ai => ai.AzureAdObjectIdentifier == claim.Value);
        }

        /// <summary>
        /// Retrieves an <see cref="ApplicationUser"/> from the database by looking up the passed condition.
        /// </summary>
        /// <param name="lookupCondition">A predicate of the condition to lookup</param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        public async Task<ApplicationUser> FindUserAsync(Expression<Func<ApplicationIdentity, bool>> lookupCondition)
        {

            var returnedObject = await _dbContext.ApplicationIdentities
                .Where(lookupCondition).FirstOrDefaultAsync();

            if (returnedObject == null) return null;

            return returnedObject as ApplicationUser;
        }

        /// <summary>
        /// Retrieves an <see cref="ApplicationUser"/> from the database by looking up the 
        /// AzureAdObjectIdentifier. Matches a user by the object identifier claim.
        /// </summary>
        /// <param name="azureAdObjectIdentifier">The value of the object identifier claim to lookup.</param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        [Obsolete("Replaced with FindUserAsync")]
        public async Task<ApplicationUser> FindUserByObjectIdAsync(string azureAdObjectIdentifier)
        {
            if (string.IsNullOrWhiteSpace(azureAdObjectIdentifier)) return null;

            var returnedObject = await _dbContext.ApplicationIdentities.Where
                (u => u.AzureAdObjectIdentifier == azureAdObjectIdentifier).FirstOrDefaultAsync();

            var returnUser = returnedObject as ApplicationUser;
            return returnUser;
        }

        /// <summary>
        /// Retrieves an <see cref="ApplicationUser"/> from the database by looking up the 
        /// UPN. Matches a user by the UPN claim.
        /// </summary>
        /// <param name="upn">The value of the UPN claim to lookup.</param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        [Obsolete("Replaced with FindUserAsync")]
        public async Task<ApplicationUser> FindUserByUpnAsync(string upn)
        {
            if (string.IsNullOrWhiteSpace(upn)) return null;

            var returnedObject = await _dbContext.ApplicationIdentities
                .Where(u => ((ApplicationUser)u).Upn == upn)
                .FirstOrDefaultAsync();

            var returnUser = returnedObject as ApplicationUser;
            return returnUser;
        }

        /// <summary>
        /// Attempts to Find a user by the object identifier claim.
        /// </summary>
        /// <param name="azureAdObjectIdentifier">The value of the object identifier claim to lookup.</param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        public async Task<ApplicationUser> EnsureUserByObjectIdAsync(string azureAdObjectIdentifier)
        {
            // TODO: implement EnsureUserByUpnAsync and EnsureUserByObjectIdAsync with Func!!
            var existingUser = await FindUserAsync(ai => ai.AzureAdObjectIdentifier == azureAdObjectIdentifier);
            if (existingUser != null)
            {
                return existingUser;
            }
            else
            {
                var currentUser = await GetCurrentUser();
                var resolvedUser = await _graphService.ResolveUserByObjectIdAsync(azureAdObjectIdentifier, currentUser.AzureAdObjectIdentifier);

                if (resolvedUser == null)
                {
                    return null;// not sure if we should rather throw
                }

                var result = _dbContext.ApplicationIdentities.Add(resolvedUser);

                // BUG: this code doesn't call SaveChangesAsync  TODO

                existingUser = resolvedUser;
            }

            return existingUser;
        }

        /// <summary>
        /// Attempts to Find a user by the UPN claim.
        /// </summary>
        /// <param name="upn">The value of the UPN claim to lookup.</param>
        /// <returns>A Task with the <see cref="ApplicationUser"/> as a result</returns>
        public async Task<ApplicationUser> EnsureUserByUpnAsync(string upn)
        {
            // NOTE: this doesn't really ensure anything - it resolves it from the graph API, then
            // returns it. Most likely it should be persisted and returned.

            // TODO: implement EnsureUserByUpnAsync and EnsureUserByObjectIdAsync with Func!!
            var existingUser = await FindUserAsync(ai => ((ApplicationUser)ai).Upn == upn);
            if (existingUser != null)
            {
                return existingUser;
            }
            else
            {
                var currentUser = await GetCurrentUser();
                var resolvedUser = await _graphService.ResolveUserByUpnAsync(upn, currentUser.AzureAdObjectIdentifier);

                if (resolvedUser == null)
                {
                    // TODO: LOG
                    return null;// not sure if we should rather throw
                }

                var result = _dbContext.ApplicationIdentities.Add(resolvedUser);

                // BUG: this code doesn't call SaveChangesAsync  TODO

                return resolvedUser;
            }
        }

        // TODO : tests
        /// <summary>
        /// Checks if the current logged in user is a ystem administrator
        /// </summary>
        /// <returns>True if the current user is a system administrator, false of not.</returns>
        public async Task<bool> IsCurrentUserAdmin()
        {
            if (_isCurrentUserAdmin != null && _isCurrentUserAdmin.HasValue)
            {
                return _isCurrentUserAdmin.Value;
            }

            var currentUser = await GetCurrentUser();

            if (currentUser == null) return false;

            var adminUser = await _dbContext.SystemAdministrators
                .Where(u => u.Identity == currentUser)
                .FirstOrDefaultAsync();

            if (adminUser != null)
            {
                _isCurrentUserAdmin = true;
                return true;
            }
            else
            {
                _isCurrentUserAdmin = false;
                return false;
            }
        }

        /// <summary>
        /// Sets an ApplicationUser as a System Addministrator
        /// </summary>
        /// <param name="applicationUser">The ApplicationUser to set as a System Administrator</param>
        /// <returns>True if the operation succeeded or the current user is already a System Administrator, otherwise False.</returns>
        public async Task<bool> SetSystemAdministrator(ApplicationUser applicationUser)
        {
            // Create a new SystemAdministrator entity and set the Identity property
            var newSystemAdministrator = new SystemAdministrator
            {
                Identity = applicationUser ?? throw new ArgumentNullException(nameof(applicationUser))
            };

            // check if exists
            if (await IsCurrentUserAdmin()) return true;

            // persist
            var createdSystemAdmin = await _dbContext.SystemAdministrators.AddAsync(newSystemAdministrator);
            var changedRows = await _dbContext.SaveChangesAsync();

            if (changedRows > 1)
            {
                // if rows > 1 we have a scope leek and another entity is also persisted
                // TODO LOG
                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes an ApplicationUser as a System Addministrator
        /// </summary>
        /// <param name="applicationUser">The ApplicationUser to remove as a System Administrator</param>
        /// <returns>True if the operation succeeded, otherwise False.</returns>
        public async Task<bool> RemoveSystemAdministrator(ApplicationUser applicationUser)
        {
            if (applicationUser == null)
            {
                throw new ArgumentNullException(nameof(applicationUser));
            }

            var systemAdministratorsList = await _dbContext.SystemAdministrators
                .Where(u => u.Identity == applicationUser)
                .ToListAsync();

            if (systemAdministratorsList == null || systemAdministratorsList.Count == 0)
            {
                _isCurrentUserAdmin = null;
                return true;
            }

            foreach (var systemAdministrator in systemAdministratorsList)
            {
                _dbContext.SystemAdministrators.Remove(systemAdministrator);
            }

            var changedRows = await _dbContext.SaveChangesAsync();
            _isCurrentUserAdmin = null;

            if (changedRows > 1)
            {
                // if rows > 1 we have a scope leek and another entity is also persisted
                // TODO LOG
                return false;
            }

            return true;
        }
    }
}
