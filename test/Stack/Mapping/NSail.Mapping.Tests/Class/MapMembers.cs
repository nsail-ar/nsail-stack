// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Class;

// Ported from Detached.Mappers.Tests/Class/Members/MemberPairSourceAnnotatedFrom.cs. Its twin,
// MemberPairSourceAnnotatedTo.cs, puts the pairing on the source; here the entity names its
// message and never the reverse, so that one is not ported.
public sealed class MapMembers
{
    [Fact]
    public void configure_member_pair_source_annotated()
    {
        var user = Mappings.Map<UserDto, User>(new UserDto { Key = 1, UserName = "leo" });

        Assert.Equal(1, user.Id);
        Assert.Equal("leo", user.Name);
    }

    [MapFrom(typeof(UserDto))]
    public sealed class User
    {
        [MapFrom(typeof(UserDto), "Key")]
        public int Id { get; set; }

        [MapFrom(typeof(UserDto), "UserName")]
        public string? Name { get; set; }

        public DateTime ModifiedDate { get; set; }
    }

    public sealed class UserDto
    {
        public int Key { get; set; }

        public string? UserName { get; set; }
    }
}
