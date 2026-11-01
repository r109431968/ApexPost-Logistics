# ADR-005: Separate domain and persistence models

Status: accepted (Week 3)

**Context.** The domain classes have private setters, value objects (`Money`, `Sku`, `BranchCode`) and rules in their methods. EF Core entities need public setters, keys and navigation properties.

**Decision.** Keep two sets of classes. The domain classes carry no EF attributes. `Persistence/Sql/Entities` holds plain classes shaped like the tables, `Persistence/Sql/Configurations` holds the fluent mapping, and `DomainMappers` converts in both directions. Value converters (registered once in `ConfigureConventions`) store `Money` as `decimal(18,2)`, `Sku` as `varchar(8)` and `BranchCode` as `varchar(5)`. Reading a row rebuilds the domain object through its public constructor, so the rules run again; a corrupted row fails loudly instead of creating an invalid object.

**Consequences.** More code (mappers) in exchange for a domain that does not know the database and can change independently. One known limit: `ExecuteUpdateAsync` cannot calculate on a converted column, so the new `Money` value is computed in C# and written as a constant.

**Alternatives.** Map the domain classes directly (rejected: private setters, constructors with rules and value objects fight EF Core conventions; the domain would get infrastructure noise).
