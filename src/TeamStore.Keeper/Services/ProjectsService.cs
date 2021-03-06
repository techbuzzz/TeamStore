﻿namespace TeamStore.Keeper.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Extensions;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TeamStore.Keeper.DataAccess;
    using TeamStore.Keeper.Interfaces;
    using TeamStore.Keeper.Models;

    /// <summary>
    /// Responsible for CRUD operations of Project.
    /// </summary>
    public class ProjectsService : IProjectsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IEncryptionService _encryptionService;
        private readonly IEventService _eventService;
        private readonly IPermissionService _permissionService;
        private readonly IApplicationIdentityService _applicationIdentityService;

        /// <summary>
        /// Constructor for the Project Service
        /// </summary>
        /// <param name="context"></param>
        /// <param name="encryptionService"></param>
        public ProjectsService(
            ApplicationDbContext context,
            IEncryptionService encryptionService,
            IEventService eventService,
            IApplicationIdentityService applicationIdentityService,
            IPermissionService permissionService)
        {
            _dbContext = context ?? throw new ArgumentNullException(nameof(context));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _applicationIdentityService = applicationIdentityService ?? throw new ArgumentNullException(nameof(applicationIdentityService));
        }

        /// <summary>
        /// Gets all projects for which the current user has access to. Excludes archived projects.
        /// </summary>
        /// <returns>A list of Project objects</returns>
        public async Task<List<Project>> GetProjects(bool skipDecryption = false)
        {
            // Get user to validate access
            var currentUser = await _applicationIdentityService.GetCurrentUser();
            if (currentUser == null) throw new Exception("Unauthorised requests are not allowed.");

            if (await _applicationIdentityService.IsCurrentUserAdmin())
            {
                return await GetProjectsForAdmin(skipDecryption);
            }

            // Get projects with access
            // TODO: attempt to make this in 1 query
            var projects = await _dbContext.Projects.Where(p =>
                p.IsArchived == false)
                .Include(p => p.AccessIdentifiers)
                .ToListAsync();

            var projectsWithAccess = projects.Where(p =>
                p.AccessIdentifiers.Any(ai => ai.Identity != null && ai.Identity.Id == currentUser.Id))
                .ToList();

            if (projectsWithAccess == null) return null;

            if (skipDecryption == false)
            {
                foreach (var project in projectsWithAccess)
                {
                    DecryptProject(project);
                }
            }

            return projectsWithAccess.ToList();
        }

        /// <summary>
        /// Retrieves a Project by Project Id, if the user has access to it.
        /// </summary>
        /// <param name="projectId">The Project Id to lookup.</param>
        /// <returns>A Project object, null if none are found or the current user does not have access to it.</returns>
        public async Task<Project> GetProject(int projectId)
        {
            return await GetProject(projectId, false);
        }

        /// <summary>
        /// Retrieves a Project by Project Id, if the user has access to it.
        /// </summary>
        /// <param name="projectId">The Project Id to lookup.</param>
        /// <param name="skipDecryption">Set to True if the project should not be decrypted.</param>
        /// <returns>A Project object, null if none are found or the current user does not have access to it.</returns>
        public async Task<Project> GetProject(int projectId, bool skipDecryption = false)
        {
            // Validate request
            if (projectId < 0) throw new ArgumentException("You must pass a valid project id.");

            // Validate access
            var currentUser = await _applicationIdentityService.GetCurrentUser();
            if (currentUser == null) throw new Exception("Unauthorised requests are not allowed."); ;

            // Get project - ensures user has access to it
            var result = await _dbContext.Projects.Where(p =>
                p.Id == projectId &&
                p.IsArchived == false &&
                p.AccessIdentifiers.Any(ai => ai.Identity.Id == currentUser.Id))
                .Include(p => p.AccessIdentifiers)
                .ThenInclude(p => p.Identity) // NOTE: intellisense doesn't work here (23.09.2017) https://github.com/dotnet/roslyn/issues/8237
                .FirstOrDefaultAsync();

            if (result == null) return null;

            // this line makes sure that the retrieved project is not retrieved
            // from EF's cache, as it will go through decryption and fail
            // It might be better to have a decrypted status rather than tinker with EF's state
            _dbContext.Entry(result).State = EntityState.Unchanged;

            if (skipDecryption == false) // decrypt project
            {
                DecryptProject(result);
            }

            return result;
        }

        /// <summary>
        /// Retrieves all projects, ignoring any access identifier restrictions.
        /// Used for a database export only.
        /// </summary>
        /// <param name="skipDecryption">Wether to return encrypted projects</param>
        /// <returns>A Task result of a List of Projects.</returns>
        private async Task<List<Project>> GetProjectsForAdmin(bool skipDecryption = false)
        {
            var projects = await _dbContext.Projects.Where(p =>
                p.IsArchived == false)
                .Include(p => p.AccessIdentifiers)
                .ToListAsync();

            if (skipDecryption == false)
            {
                foreach (var project in projects)
                {
                    DecryptProject(project);
                }
            }

            return projects.ToList();
        }

        /// <summary>
        /// Decrypts all project properties.
        /// </summary>
        /// <param name="result">The Project to decrypt</param>
        private void DecryptProject(Project result)
        {
            result.Title = _encryptionService.DecryptString(result.Title);
            result.Description = _encryptionService.DecryptString(result.Description);
            result.Category = _encryptionService.DecryptString(result.Category);
        }

        /// <summary>
        /// Encrypts and persists a Project in the database.
        /// </summary>
        /// <param name="decryptedProject">The Project object to encrypt and persist</param>
        /// <returns>A Task of int with the Project Id.</returns>
        public async Task<int> CreateProject(Project project)
        {
            // Validate title
            if (string.IsNullOrWhiteSpace(project.Title)) throw new ArgumentException("A project must have a title.");

            // Encrypt
            project.Title = _encryptionService.EncryptString(project.Title);
            project.Description = _encryptionService.EncryptString(project.Description);
            project.Category = _encryptionService.EncryptString(project.Category);

            var currentUser = await _applicationIdentityService.GetCurrentUser();
            if (currentUser == null) throw new Exception("Unauthorised requests are not allowed."); // we fail on no current user

            // Ensure the creating user has Owner permissions to be able to grant access to other users
            // It is important to distinguish between creating through a UI call vs importing projects
            // This method is used in both cases
            if (project.AccessIdentifiers.Any(ai=>ai.Identity?.AzureAdObjectIdentifier == currentUser.AzureAdObjectIdentifier) == false)
            {
                project.AccessIdentifiers.Add(new AccessIdentifier()
                {
                    Identity = currentUser,
                    Role = Enums.Role.Owner,
                    Project = project
                });
            }

            // Set any AccessIdentifier statuses
            foreach (var accessItem in project.AccessIdentifiers)
            {
                accessItem.Created = DateTime.UtcNow;
                accessItem.CreatedBy = currentUser;
                // Modified is not set in the create routine

                // Access Identifier Validation
                if (accessItem.CreatedBy == null) throw new ArgumentException("The current user could not be resolved during project creation.");
            }

            // Save
            await _dbContext.Projects.AddAsync(project);
            var updatedRowCount = await _dbContext.SaveChangesAsync(); // returns 2 or 3 (currentUser)

            // LOG event TODO

            return project.Id;
        }

        /// <summary>
        /// Imports and persists a Project into the database.
        /// This is designed to be used by a database import.
        /// </summary>
        /// <param name="decryptedProject">The Project object to encrypt and persist</param>
        /// <returns>A Task of int with the Project Id.</returns>
        public async Task<int> ImportProject(Project project)
        {
            // reset all Id's of the entity hierarchy to avoid primary key conflicts
            project.Id = 0;
            project.AccessIdentifiers.All(ai => { ai.Id = 0; return true; });
            project.Assets.All(a => { a.Id = 0; return true; });

            // this logic will fail if we persist a project with decrypted assets
            // thus we just run through the decryptor to check and allow it to throw on import
            // if the assets are decrypted, rather than persist decrypted
            foreach (var asset in project.Assets)
            {
                _encryptionService.DecryptString(asset.Title); // will throw if decrypted
            }

            // if this is a fresh database the current user might not exists yet
            // causing double tracking to exist in EF, which throws.
            // the workaround is to save the user, so it has a valid Id
            var currentUser = await _applicationIdentityService.GetCurrentUser();
            if (currentUser.Id == 0)
            {
                _dbContext.ApplicationIdentities.Add(currentUser);
                await _dbContext.SaveChangesAsync(); // save the current user in the database
            }

            return await CreateProject(project);
        }

        /// <summary>
        /// Discards all tracked changes to the entity and marks it as archived in the database
        /// </summary>
        /// <param name="decryptedProject">The Project entity to archive.</param>
        /// <param name="remoteIpAddress">The IP address of the request causing the event</param>
        /// <returns>A Task result</returns>
        public async Task ArchiveProject(Project decryptedProject, string remoteIpAddress)
        {
            // Validation
            if (decryptedProject == null) throw new ArgumentException("You must pass a valid project.");

            // TODO: ensure the current user has access to archive this project
            var currentUser = await _applicationIdentityService.GetCurrentUser();
            if (currentUser == null) throw new Exception("Unauthorised requests are not allowed."); // we fail on no current user

            // Refresh the entity to discard changes and avoid saving a decrypted project
            _dbContext.Entry(decryptedProject).State = EntityState.Unchanged;

            // Refresh assets and set to archived
            foreach (var asset in decryptedProject.Assets)
            {
                _dbContext.Entry(asset).State = EntityState.Unchanged;
                asset.IsArchived = true;
            }

            decryptedProject.IsArchived = true; // set archive status

            await _eventService.LogArchiveProjectEventAsync(decryptedProject.Id, currentUser.Id, remoteIpAddress);

            var updatedRowCount = await _dbContext.SaveChangesAsync(); // save to db
            if (updatedRowCount > 1)
            {
                // we have a problem
            }
        }
    }
}
