# Contributing

There are developers who maintain Generellem and will continue to do so. However, the project is open to participation from anyone that wants to make a contribution. This guide discusses items that everyone should be aware of in the contribution process. We'll discuss different ways to contribute, coordination process, and how to submit code.

## Types of contributions

There are many ways to contribute such as code documentation, and helping others. The following sections discuss this more.

### New Features

There are two ways to see existing new features: [The Generellem Project](https://github.com/users/generellem/projects/2) or [Issues](https://github.com/JoeMayo/generellem/issues). By design, we turn all new project cards into Issues so they show up in the Issues too, where it might be easier for some people to read details. Of course, reading via the Project gives a good idea of each issue's status.

If there's an issue that you would like to work on, reach out to Joe so he can assign that to you. If you don't see a feature that you would like to have, either create a new one in Issues or visit the Discussions section if you want to discuss it with other community members first.

Really, we need at least some type of governance over new issues. Coordinating these things helps people from wasting a lot of time going in a direction that doesn't fit the project. Communicating about it is better. Also, it keeps multiple people from working on the same feature at the same time.

If you're a new developer, look for the issues tagged as "good first issue". Of course, if you just want to jump in head first go-for-it. Please coordinate and ask for help so you don't flounder too long on a single issue. We want to see you succeed.

### Bug Fixes

In many ways, the bug fix process is similar to New Features in that we want to coordinate and track each fix. Keeping track of bugs also a metric that helps measure the health and quality of the system. Please search open and closed issues to see if the bug has already been addressed before submitting a new issue.

### Unit Tests

You can run code coverage on Generellem test and might find gaps that we should have written a unit test for. Similar to New Feature, please coordinate. You want to avoid writing a unit test in code that someone else is currently working on, where they would normally be expected to write those tests.

### Documentation

The getting-started guide is the README. This file, `CONTRIBUTING.md`, sits in the repository root so GitHub can link it from the project page. The wiki should match the README and should not send people through a separate setup. The `Documentation` folder is sample content for the demo, not the developer guide.

You're welcome to update the documentation in any way that would improve it. Even spelling corrections and grammar are fine. The standard is US English so we don't accidentally change back and forth between other forms. For Markdown in the repository, submit a PR, explained below. For the wiki, small spelling or grammatical fixes are fine as you go. Coordinate an extensive change through a Discussion or an Issue.

>Important! All documentation must be original - not AI generated. The rationale is that Generellem uses the documentation as proof of concept of how it works by making non-public data consumable. AI generated data pollutes the data source.

### Helping Others

There are various ways to help others. The first is by participating in Issues and Discussions. You can ask questions, answer other people's questions, or just generally participate in any discussion - as long as it's on topic for Generellem. New developers might want to take on an Issue and might need help. In other cases, there might be a new feature of such significant size the people might want to form a team to coordinate the work. The sky is the limit here and everything that you and others do contributes to the community and helps you achieve whatever goals you set out for yourself.

## Handling Issues

Everything is documented on an Issue. Although the Projects feature creates a card, we should change cards to Issues, which have more options like status, tagging, and conversations. It's also advantageous that an Issue appears in both the Projects and Issues pages, making them more visible to people who might not look in another location.

The conversation about an Issue should happen on the issue, rather than Discussions, email, or anywhere else. That creates a history of what we were thinking at that point in time, which matters because people usually have questions about why something was done a certain way. We can look back in time and either see an explanation or make assumptions about what technology might have been most appropriate at that point in time.

When you take an Issue, take ownership of it. This means you ensure you are assigned, the status is up-to-date, all fields are properly updated, and any other information about that issue is recorded. As alluded to in the previous paragraph, the issue is a historical record of what we were thinking at the time and why we made the decisions we did.

## Submitting Code

Let's say you found something you want to work on, you coordinated the decision, and you have the code written. The next step is to submit that code so it goes into the repository. The `main` branch is locked, so you can't just check into it. This is intentional to prevent people from accidentally checking in breaking changes. The following items outline a safer approach:

1. You need to have a separate branch to work on. If you accidentally started working on the `main` branch, use Git to figure out how to get your changes into a new branch. If you're using Visual Studio 2022, this is easy because if you have files in your current branch, when you create and checkout a new branch, Visual Studio will ask if you want to move your changes to the new branch.
2. Write Unit Tests. You can look at the test project source code to pick up the style of test being used. We use XUnit as the test framework and Moq for a mocking library. A PR without unit tests is unlikely to be approved until tests are written. Unit tests are part of the code.
3. Push your branch and create a new PR. The PR has a title, description, and Issue reference. The title is a short description of what the PR is about. The description explains the work that was done. Imagine you are a developer looking at file history and need to read the details of a commit - what would you appreciate to read? The expectation that you've coordinated the work assumes that there is an Issue associated with the code. You should reference the Issue with the hashtag, such as in #42, where the hashtag says you're referencing an issue and 42 is the issue number.
4. PR is an acronym for Peer Review. This means that you shouldn't automatically merge without a second review. Code reviews are a best practice in software engineering. They help by identifying problems, sharing knowledge, and maintaining consistent project standards. We'll try to be responsive to PRs and make sure they don't linger too long. If someone hasn't reviewed your PR within a couple of days, bump it in discussion, issue, or just reach out to someone as a reminder. BTW, smaller PRs are much easier for people to review. If you have a giant PR, it can take a long time to review. That said, all code that gets checked in must work, which is another benefit of writing unit tests.
5. After someone approves your PR, you can merge. You should use a squash merge, which reduces the amount of commit noise in history. You should delete your branch after merging so we don't have a bunch of stale branches cluttering the repository.

## Summary

This article started out by welcoming anyone who would like to contribute to Generellem. While a lot of people believe that contributing to open source is primary code, remember that there are many ways to contribute, such as working on documentation and various ways to help other people. After you've written code or modified documentation that is part of the source code, you should submit a PR. This document explained the process to submit code via a PR and explained why certain steps are important. Finally, remember that this all improves with community participation and constructive feedback is welcome.
