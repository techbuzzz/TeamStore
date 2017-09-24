﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using System;
using TeamStore.Keeper.DataAccess;
using TeamStore.Keeper.Enums;

namespace TeamStore.Keeper.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20170924104341_AssetUpdates")]
    partial class AssetUpdates
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.0-rtm-26452");

            modelBuilder.Entity("TeamStore.Keeper.Models.AccessIdentifier", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("Created");

                    b.Property<int?>("CreatedById");

                    b.Property<int?>("IdentityId");

                    b.Property<DateTime>("Modified");

                    b.Property<int?>("ModifiedById");

                    b.Property<int>("ProjectForeignKey");

                    b.Property<string>("Role");

                    b.HasKey("Id");

                    b.HasIndex("CreatedById");

                    b.HasIndex("IdentityId");

                    b.HasIndex("ModifiedById");

                    b.HasIndex("ProjectForeignKey");

                    b.ToTable("AccessIdentifiers");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.ApplicationIdentity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AzureAdObjectIdentifier");

                    b.Property<string>("Discriminator")
                        .IsRequired();

                    b.Property<string>("DisplayName");

                    b.Property<string>("TenantId");

                    b.HasKey("Id");

                    b.ToTable("ApplicationIdentities");

                    b.HasDiscriminator<string>("Discriminator").HasValue("ApplicationIdentity");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Asset", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("Created");

                    b.Property<int?>("CreatedById");

                    b.Property<string>("Discriminator")
                        .IsRequired();

                    b.Property<bool>("IsEnabled");

                    b.Property<DateTime>("Modified");

                    b.Property<int?>("ModifiedById");

                    b.Property<int>("ProjectForeignKey");

                    b.HasKey("Id");

                    b.HasIndex("CreatedById");

                    b.HasIndex("ModifiedById");

                    b.HasIndex("ProjectForeignKey");

                    b.ToTable("Assets");

                    b.HasDiscriminator<string>("Discriminator").HasValue("Asset");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Event", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("ActedByUserId");

                    b.Property<int?>("AssetForeignKey");

                    b.Property<string>("Data");

                    b.Property<DateTime>("DateTime");

                    b.Property<string>("NewValue");

                    b.Property<string>("OldValue");

                    b.Property<string>("RemoteIpAddress");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.HasIndex("ActedByUserId");

                    b.HasIndex("AssetForeignKey");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Project", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Category");

                    b.Property<string>("Description");

                    b.Property<bool>("IsArchived");

                    b.Property<string>("Title");

                    b.HasKey("Id");

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.ApplicationGroup", b =>
                {
                    b.HasBaseType("TeamStore.Keeper.Models.ApplicationIdentity");


                    b.ToTable("ApplicationGroup");

                    b.HasDiscriminator().HasValue("ApplicationGroup");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.ApplicationUser", b =>
                {
                    b.HasBaseType("TeamStore.Keeper.Models.ApplicationIdentity");

                    b.Property<string>("AzureAdName");

                    b.Property<string>("AzureAdNameIdentifier");

                    b.Property<string>("Upn");

                    b.ToTable("ApplicationUser");

                    b.HasDiscriminator().HasValue("ApplicationUser");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Credential", b =>
                {
                    b.HasBaseType("TeamStore.Keeper.Models.Asset");

                    b.Property<string>("Domain");

                    b.Property<string>("Login");

                    b.ToTable("Credential");

                    b.HasDiscriminator().HasValue("Credential");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Note", b =>
                {
                    b.HasBaseType("TeamStore.Keeper.Models.Asset");

                    b.Property<string>("Body");

                    b.Property<string>("Title");

                    b.ToTable("Note");

                    b.HasDiscriminator().HasValue("Note");
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.AccessIdentifier", b =>
                {
                    b.HasOne("TeamStore.Keeper.Models.ApplicationUser", "CreatedBy")
                        .WithMany()
                        .HasForeignKey("CreatedById");

                    b.HasOne("TeamStore.Keeper.Models.ApplicationIdentity", "Identity")
                        .WithMany()
                        .HasForeignKey("IdentityId");

                    b.HasOne("TeamStore.Keeper.Models.ApplicationUser", "ModifiedBy")
                        .WithMany()
                        .HasForeignKey("ModifiedById");

                    b.HasOne("TeamStore.Keeper.Models.Project", "Project")
                        .WithMany("AccessIdentifiers")
                        .HasForeignKey("ProjectForeignKey")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Asset", b =>
                {
                    b.HasOne("TeamStore.Keeper.Models.ApplicationUser", "CreatedBy")
                        .WithMany()
                        .HasForeignKey("CreatedById");

                    b.HasOne("TeamStore.Keeper.Models.ApplicationUser", "ModifiedBy")
                        .WithMany()
                        .HasForeignKey("ModifiedById");

                    b.HasOne("TeamStore.Keeper.Models.Project", "Project")
                        .WithMany("Assets")
                        .HasForeignKey("ProjectForeignKey")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("TeamStore.Keeper.Models.Event", b =>
                {
                    b.HasOne("TeamStore.Keeper.Models.ApplicationUser", "ActedByUser")
                        .WithMany()
                        .HasForeignKey("ActedByUserId");

                    b.HasOne("TeamStore.Keeper.Models.Asset", "Asset")
                        .WithMany()
                        .HasForeignKey("AssetForeignKey");
                });
#pragma warning restore 612, 618
        }
    }
}
