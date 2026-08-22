# ADR 0005 - Defer support-microservice selection to the group

Status: Accepted as a scope boundary  
Date: 2026-08-21

## Context

The professor requires a support microservice, but the group has not selected
authentication, notification, e-mail, SMS, or another option. That requirement is
broader than the Order service.

## Decision

Support-microservice selection and implementation are not part of the current
Order core. Order reserves an optional `OrderCreated` integration-event shape and
outbox extension point but does not select a broker or consumer yet.

## Consequences

- PRD, SDD, domain work, REST API, SQL Server, and Docker planning can proceed.
- The group must close the rubric item before final integration/presentation.
- No unused AWS messaging service is forced into the first Order implementation.
