namespace IntegrationTests
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using TeamStore.Keeper.Models;
    using TeamStore.Keeper.Services;
    using Xunit;

    public class ProjectTests : IntegrationTestBase
    {
        public ProjectTests()
        {

        }

        /// <summary>
        /// Creates a project, then retrieves it. Validates that the properties are the same.
        /// Goes through encryption
        /// </summary>
        [Fact]
        public async void CreateProject_ShouldReturnCorrectData()
        {
            // Arrange
            _fakeHttpContextItems.Add(ApplicationIdentityService.CURRENTUSERKEY, _testUser);
            string testTitle = "Project 1234 Test";
            string testDescription = "Created during integration tests";
            string testCategory = "Integration Tests";
            Project newDecryptedProject = new Project();
            newDecryptedProject.Title = testTitle;
            newDecryptedProject.Description = testDescription;
            newDecryptedProject.Category = testCategory;

            // Act
            var createdProjectId = await _projectsService.CreateProject(newDecryptedProject);
            var retrievedProject = await _projectsService.GetProject(createdProjectId);

            // Assert
            Assert.Equal(retrievedProject.Title, testTitle);
            Assert.Equal(retrievedProject.Description, testDescription);
            Assert.Equal(retrievedProject.Category, testCategory);
            Assert.True(retrievedProject.Id > 0);

            // Cleanup
            await _projectsService.ArchiveProject(retrievedProject);
            var archivedProject = await _projectsService.GetProject(createdProjectId);
            Assert.Null(archivedProject);
        }

        /// <summary>
        /// Validates that a project wiith no title throws.
        /// </summary>
        [Fact]
        public async void CreateProject_ShouldFailOnEmptyTitle()
        {
            // Arrange
            Project newDecryptedProject = new Project();

            // Act
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _projectsService.CreateProject(newDecryptedProject);
            });
        }

        /// <summary>
        /// Tests projet access identifier storage and retrieval.
        /// </summary>
        [Fact]
        public async void CreateProject_ShouldReturnCorrectAccess()
        {
            // Arrange
            _fakeHttpContextItems.Add(ApplicationIdentityService.CURRENTUSERKEY, _testUser);

            var newDecryptedProject = CreateTestProject();

            AccessIdentifier accessIdentifier1 = new AccessIdentifier();
            accessIdentifier1.Role = "Test";
            accessIdentifier1.Identity = new ApplicationUser()
            {
                AzureAdNameIdentifier = "TestAdIdentity1",
                AzureAdObjectIdentifier = "TestAdObjectId1",
                TenantId = "1234",
                Upn = "test@test.com"
            };

            AccessIdentifier accessIdentifier2 = new AccessIdentifier();
            accessIdentifier2.Role = "Admin";
            accessIdentifier2.Identity = new ApplicationGroup()
            {
                DisplayName = "TestGroup1",
                AzureAdObjectIdentifier = "TestAdObjectId1",
                TenantId = "1234"
            };

            newDecryptedProject.AccessIdentifiers.Add(accessIdentifier1);
            newDecryptedProject.AccessIdentifiers.Add(accessIdentifier2);

            // Act
            var createdProjectId = await _projectsService.CreateProject(newDecryptedProject);
            var retrievedProject = await _projectsService.GetProject(createdProjectId);

            // Assert
            Assert.True(retrievedProject.Id > 0);
            Assert.False(retrievedProject.IsArchived);
            Assert.Equal(3, retrievedProject.AccessIdentifiers.Count); // +1 for the Owner
            Assert.NotNull(retrievedProject.AccessIdentifiers.First());
            Assert.NotNull(retrievedProject.AccessIdentifiers.First().Project);
            Assert.True(retrievedProject.AccessIdentifiers.First().ProjectForeignKey > 0);
            Assert.Equal("Test", retrievedProject.AccessIdentifiers.First().Role);
            Assert.NotEqual("Test1234", retrievedProject.AccessIdentifiers.First().Role);
            Assert.IsType<ApplicationUser>(retrievedProject.AccessIdentifiers.First().Identity);
            Assert.Equal("1234", retrievedProject.AccessIdentifiers.First().Identity.TenantId);
            Assert.Equal("TestAdObjectId1", retrievedProject.AccessIdentifiers.First().Identity.AzureAdObjectIdentifier);
            Assert.Equal("test@test.com", ((ApplicationUser)retrievedProject.AccessIdentifiers.First().Identity).Upn);
            Assert.Equal("TestAdIdentity1", ((ApplicationUser)retrievedProject.AccessIdentifiers.First().Identity).AzureAdNameIdentifier);

            // Cleanup
            await _projectsService.ArchiveProject(retrievedProject);
            var archivedProject = await _projectsService.GetProject(createdProjectId);
            Assert.Null(archivedProject);
        }

        [Fact]
        public async void GetProjects_ShouldGiveProjectsWithRightAccess()
        {
            // Arrange
            _fakeHttpContextItems.Add(ApplicationIdentityService.CURRENTUSERKEY, _testUser);
            var newDecryptedProject = CreateTestProject();
            var newAzureAdObjectId = "newazureaduser";

            // Act
            var createdProjectId = await _projectsService.CreateProject(newDecryptedProject);
            var retrievedProject = await _projectsService.GetProject(createdProjectId);
            await _permissionService.GrantAccessAsync(retrievedProject.Id, newAzureAdObjectId, "Edit", "1.2.3.4", _projectsService);
            
            // Assert
        }

        private Project CreateTestProject()
        {
            string testTitle = "Project 1234 Test";
            string testDescription = "Created during integration tests";
            string testCategory = "Category Tests";
            Project newDecryptedProject = new Project();
            newDecryptedProject.Title = testTitle;
            newDecryptedProject.Description = testDescription;
            newDecryptedProject.Category = testCategory;

            return newDecryptedProject;
        }
    }
}