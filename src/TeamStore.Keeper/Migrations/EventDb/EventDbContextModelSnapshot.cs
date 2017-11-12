﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using TeamStore.Keeper.DataAccess;

namespace TeamStore.Keeper.Migrations.EventDb
{
    [DbContext(typeof(EventDbContext))]
    partial class EventDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.0-rtm-26452");

            modelBuilder.Entity("TeamStore.Keeper.Models.Event", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ActedByUser");

                    b.Property<int?>("AssetId");

                    b.Property<string>("Data");

                    b.Property<DateTime>("DateTime");

                    b.Property<string>("NewValue");

                    b.Property<string>("OldValue");

                    b.Property<int?>("ProjectId");

                    b.Property<string>("RemoteIpAddress");

                    b.Property<int?>("TargetUserId");

                    b.Property<string>("Type");

                    b.HasKey("Id");

                    b.ToTable("Events");
                });
#pragma warning restore 612, 618
        }
    }
}
